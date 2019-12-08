using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Bot.Abstractions;
using Helix.Core;
using log4net;
using Microsoft.Extensions.Logging;

namespace Helix.Bot
{
    public class HtmlRendererBlock : TransformBlock<Tuple<IHtmlRenderer, Resource>, RenderingResult>,
                                     IHtmlRendererBlock, IDisposable
    {
        readonly CancellationTokenSource _cancellationTokenSource;

        public BufferBlock<Event> Events { get; }

        public BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        public BufferBlock<IHtmlRenderer> HtmlRenderers { get; }

        public BufferBlock<(ReportWritingAction, VerificationResult)> ReportWritingMessages { get; }

        public override Task Completion => Task.WhenAll(
            base.Completion,
            Events.Completion,
            HtmlRenderers.Completion,
            ReportWritingMessages.Completion,
            FailedProcessingResults.Completion
        );

        public HtmlRendererBlock(IStatistics statistics, IProcessedUrlRegister processedUrlRegister, ILog log)
            : base(maxDegreeOfParallelism: Configurations.MaxHtmlRendererCount)
        {
            _log = log;
            _statistics = statistics;
            _processedUrlRegister = processedUrlRegister;
            _cancellationTokenSource = new CancellationTokenSource();

            HtmlRenderers = new BufferBlock<IHtmlRenderer>();
            ReportWritingMessages = new BufferBlock<(ReportWritingAction, VerificationResult)>();
            FailedProcessingResults = new BufferBlock<FailedProcessingResult>();
            Events = new BufferBlock<Event>(new DataflowBlockOptions { EnsureOrdered = true });
        }

        public override void Complete()
        {
            try
            {
                base.Complete();
                _cancellationTokenSource.Cancel();

                base.Completion.Wait();
                Events.Complete();
                HtmlRenderers.Complete();
                ReportWritingMessages.Complete();
                FailedProcessingResults.Complete();
            }
            catch (Exception exception)
            {
                if (exception.IsAcknowledgingOperationCancelledException(_cancellationTokenSource.Token)) return;
                _log.Error($"One or more errors occurred while completing {nameof(HtmlRendererBlock)}.", exception);
            }
        }

        public void Dispose() { _cancellationTokenSource?.Dispose(); }

        protected override RenderingResult Transform(Tuple<IHtmlRenderer, Resource> htmlRendererAndResource)
        {
            var (htmlRenderer, resource) = htmlRendererAndResource ?? throw new ArgumentNullException(nameof(htmlRendererAndResource));
            try
            {
                if (!resource.StatusCode.IsWithinBrokenRange())
                {
                    var resourceSizeInMb = resource.Size / 1024f / 1024f;
                    if (resourceSizeInMb > Configurations.RenderableResourceSizeInMb)
                        return ProcessUnsuccessfulRendering(
                            $"Resource was not queued for rendering because it was too big ({resourceSizeInMb} MB): {resource.ToJson()}",
                            LogLevel.Debug
                        );

                    var resourceTypeIsNotRenderable = !(ResourceType.Html | ResourceType.Unknown).HasFlag(resource.ResourceType);
                    if (!resource.IsInternal || !resource.IsExtractedFromHtmlDocument || resourceTypeIsNotRenderable)
                        return ProcessUnsuccessfulRendering(null, LogLevel.None);
                }

                var oldUri = resource.Uri;
                var oldStatusCode = resource.StatusCode;
                var renderingFailed = !htmlRenderer.TryRender(
                    resource,
                    out var htmlText,
                    out var millisecondsPageLoadTime,
                    out var capturedResources,
                    _cancellationTokenSource.Token
                );

                if (renderingFailed)
                    return ProcessUnsuccessfulRendering(
                        $"Failed to render {nameof(Resource)} was discarded: {resource.ToJson()}",
                        LogLevel.Information
                    );

                var redirectHappened = resource.Uri != oldUri;
                if (redirectHappened)
                {
                    if (oldStatusCode == StatusCode.Ok)
                        _log.Info($"Redirect happened on valid resource: {resource.ToJson()}");

                    var verificationResult = resource.ToVerificationResult();
                    SendOutReportWritingMessage(
                        _processedUrlRegister.TryRegister(verificationResult.VerifiedUrl)
                            ? (ReportWritingAction.Update, verificationResult)
                            : (ReportWritingAction.RemoveAndUpdate, verificationResult)
                    );
                }

                DoStatistics();
                UpdateStatusCodeIfNecessary();
                SendOutResourceRenderedEvent();

                if (resource.StatusCode.IsWithinBrokenRange())
                    return ProcessUnsuccessfulRendering(null, LogLevel.None);

                return new RenderingResult
                {
                    RenderedResource = resource,
                    CapturedResources = capturedResources,
                    HtmlDocument = new HtmlDocument { Uri = resource.Uri, HtmlText = htmlText }
                };

                #region Local Functions

                void DoStatistics()
                {
                    if (millisecondsPageLoadTime.HasValue)
                        _statistics.IncrementSuccessfullyRenderedPageCount(millisecondsPageLoadTime.Value);

                    var newStatusCode = resource.StatusCode;
                    if (oldStatusCode.IsWithinBrokenRange() && !newStatusCode.IsWithinBrokenRange())
                        _statistics.IncrementValidUrlCountAndDecrementBrokenUrlCount();
                    else if (!oldStatusCode.IsWithinBrokenRange() && newStatusCode.IsWithinBrokenRange())
                        _statistics.DecrementValidUrlCountAndIncrementBrokenUrlCount();
                }
                void UpdateStatusCodeIfNecessary()
                {
                    var newStatusCode = resource.StatusCode;
                    if (redirectHappened || newStatusCode == oldStatusCode) return;

                    SendOutReportWritingMessage((ReportWritingAction.Update, resource.ToVerificationResult()));
                }
                void SendOutResourceRenderedEvent()
                {
                    var statisticsSnapshot = _statistics.TakeSnapshot();
                    var resourceRenderedEvent = new ResourceRenderedEvent
                    {
                        ValidUrlCount = statisticsSnapshot.ValidUrlCount,
                        BrokenUrlCount = statisticsSnapshot.BrokenUrlCount,
                        VerifiedUrlCount = statisticsSnapshot.VerifiedUrlCount,
                        Message = $"{resource.StatusCode:D} - {resource.Uri.AbsoluteUri}",
                        MillisecondsAveragePageLoadTime = statisticsSnapshot.MillisecondsAveragePageLoadTime
                    };
                    if (!Events.Post(resourceRenderedEvent) && !Events.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(Events)}].");
                }
                void SendOutReportWritingMessage((ReportWritingAction, VerificationResult) reportWritingMessage)
                {
                    if (!ReportWritingMessages.Post(reportWritingMessage) && !ReportWritingMessages.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(ReportWritingMessages)}].");
                }

                #endregion
            }
            catch (Exception exception) when (!exception.IsAcknowledgingOperationCancelledException(_cancellationTokenSource.Token))
            {
                return ProcessUnsuccessfulRendering(
                    $"One or more errors occurred while rendering: {resource.ToJson()}.",
                    LogLevel.Error,
                    exception
                );
            }
            finally
            {
                if (!HtmlRenderers.Post(htmlRenderer) && !HtmlRenderers.Completion.IsCompleted)
                    _log.Error($"Failed to post data to buffer block named [{nameof(HtmlRenderers)}].");
            }

            #region Local Functions

            RenderingResult ProcessUnsuccessfulRendering(string logMessage, LogLevel logLevel, Exception exception = null)
            {
                var failedProcessingResult = new FailedProcessingResult { ProcessedResource = resource };
                if (!FailedProcessingResults.Post(failedProcessingResult) && !FailedProcessingResults.Completion.IsCompleted)
                    _log.Error($"Failed to post data to buffer block named [{nameof(FailedProcessingResults)}].");

                switch (logLevel)
                {
                    case LogLevel.None:
                        break;
                    case LogLevel.Trace:
                    case LogLevel.Debug:
                        _log.Debug(logMessage, exception);
                        break;
                    case LogLevel.Information:
                        _log.Info(logMessage, exception);
                        break;
                    case LogLevel.Warning:
                        _log.Warn(logMessage, exception);
                        break;
                    case LogLevel.Error:
                        _log.Error(logMessage, exception);
                        break;
                    case LogLevel.Critical:
                        _log.Fatal(logMessage, exception);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
                }

                return null;
            }

            #endregion
        }

        #region Injected Services

        readonly ILog _log;
        readonly IStatistics _statistics;
        readonly IProcessedUrlRegister _processedUrlRegister;

        #endregion
    }
}