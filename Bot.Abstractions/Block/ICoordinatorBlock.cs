using System.Threading.Tasks.Dataflow;

namespace Helix.Bot.Abstractions
{
    public interface ICoordinatorBlock : IPropagatorBlock<ProcessingResult, Resource>, IReceivableSourceBlock<Resource>
    {
        BufferBlock<Event> Events { get; }

        int InputCount { get; }

        int OutputCount { get; }

        bool TryActivateWorkflow(string startUrl);
    }
}