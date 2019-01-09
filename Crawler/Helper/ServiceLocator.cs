using Helix.Crawler.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Helix.Crawler
{
    static class ServiceLocator
    {
        static ServiceProvider _serviceProvider;

        static ServiceLocator()
        {
            _serviceProvider = new ServiceCollection()
                .AddSingleton<IMemory, Memory>()
                .BuildServiceProvider();
        }

        public static void Dispose()
        {
            _serviceProvider.Dispose();
            _serviceProvider = null;
        }

        public static TService Get<TService>() { return _serviceProvider.GetService<TService>(); }

        public static void RegisterServices(Configurations configurations)
        {
            if (_serviceProvider?.GetService<Configurations>() != null) return;
            _serviceProvider?.Dispose();
            _serviceProvider = new ServiceCollection()
                .AddTransient<IWebBrowser, ChromiumWebBrowser>()
                .AddTransient<IRawResourceExtractor, RawResourceExtractor>()
                .AddTransient<IResourceVerifier, ResourceVerifier>()
                .AddTransient<IRawResourceProcessor, RawResourceProcessor>()
                .AddTransient<IResourceScope, ResourceScope>()
                .AddSingleton(configurations)
                .AddSingleton<IMemory, Memory>()
                .BuildServiceProvider();
        }
    }
}