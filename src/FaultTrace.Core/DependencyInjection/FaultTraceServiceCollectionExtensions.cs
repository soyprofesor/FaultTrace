using System;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Options;
using FaultTrace.Core.Translation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FaultTrace.Core.DependencyInjection;

/// <summary>
/// Registers FaultTrace for hosts without ASP.NET Core, such as workers.
/// </summary>
public static class FaultTraceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IFaultTranslator"/>, <see cref="IFaultFactory"/> and their services.
    /// </summary>
    /// <param name="services">The services to add to.</param>
    /// <param name="configure">Changes the settings.</param>
    /// <returns>A builder for adding providers.</returns>
    public static IFaultTraceBuilder AddFaultTraceCore(this IServiceCollection services, Action<FaultTraceOptions>? configure = null)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddOptions();
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IFaultMessageProvider, DefaultFaultMessageProvider>();
        // Registered here, so it comes before the providers added to the builder.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFaultProvider, ExceptionFaultProvider>(provider => new ExceptionFaultProvider(
            provider.GetRequiredService<IOptions<FaultTraceOptions>>(),
            provider.GetRequiredService<IFaultMessageProvider>(),
            provider.GetService<ILoggerFactory>()?.CreateLogger("FaultTrace") ?? NullLogger.Instance)));
        services.TryAddSingleton(provider => new FaultTranslator(
            provider.GetServices<IFaultProvider>(),
            provider.GetServices<IFaultHook>(),
            provider.GetRequiredService<IFaultMessageProvider>(),
            provider.GetService<ILoggerFactory>()?.CreateLogger("FaultTrace") ?? NullLogger.Instance,
            provider.GetService<IFaultRequestAccessor>()));
        services.TryAddSingleton<IFaultTranslator>(provider => provider.GetRequiredService<FaultTranslator>());
        services.TryAddSingleton<IFaultFactory>(provider => provider.GetRequiredService<FaultTranslator>());

        return new FaultTraceBuilder(services);
    }

    private sealed class FaultTraceBuilder : IFaultTraceBuilder
    {
        public FaultTraceBuilder(IServiceCollection services)
        {
            Services = services;
        }

        public IServiceCollection Services { get; }
    }
}
