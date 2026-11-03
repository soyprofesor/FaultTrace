using System;
using FaultTrace.AspNetCore.Configuration;
using FaultTrace.AspNetCore.Culture;
using FaultTrace.AspNetCore.Handling;
using FaultTrace.AspNetCore.Response;
using FaultTrace.AspNetCore.Users;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.DependencyInjection;
using FaultTrace.Core.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FaultTrace.AspNetCore.DependencyInjection;

/// <summary>
/// Registers FaultTrace for ASP.NET Core.
/// </summary>
public static class FaultTraceAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers FaultTrace. Add <c>UseFaultTrace()</c> first in the pipeline.
    /// </summary>
    /// <param name="services">The services to add to.</param>
    /// <param name="configure">Changes the settings.</param>
    /// <returns>A builder for adding providers.</returns>
    public static IFaultTraceBuilder AddFaultTrace(this IServiceCollection services, Action<FaultTraceAspNetCoreOptions>? configure = null)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var builder = services.AddFaultTraceCore();
        AddHandling(services);

        if (configure != null)
        {
            Configure(services, configure);
        }

        return builder;
    }

    /// <summary>
    /// Registers FaultTrace with settings from configuration, which reload when the configuration changes.
    /// </summary>
    /// <param name="services">The services to add to.</param>
    /// <param name="section">The section to read, for example <c>FaultTrace</c>.</param>
    /// <param name="configure">Changes the settings configuration can't hold, such as delegates and texts.</param>
    /// <returns>A builder for adding providers.</returns>
    public static IFaultTraceBuilder AddFaultTrace(
        this IServiceCollection services,
        IConfigurationSection section,
        Action<FaultTraceAspNetCoreOptions>? configure = null)
    {
        if (section == null)
        {
            throw new ArgumentNullException(nameof(section));
        }

        var builder = services.AddFaultTrace();
        services.Configure<FaultTraceOptions>(section);
        services.Configure<FaultTraceAspNetCoreOptions>(section);

        if (configure != null)
        {
            Configure(services, configure);
        }

        return builder;
    }

    // The action runs once per options type: the shared settings live in the Core options, the rest here.
    private static void Configure(IServiceCollection services, Action<FaultTraceAspNetCoreOptions> configure)
    {
        services.Configure<FaultTraceOptions>(core => configure(new FaultTraceAspNetCoreOptions(core)));
        services.Configure(configure);
    }

    private static void AddHandling(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddSingleton<FaultUserResolver>();
        services.TryAddSingleton<FaultCultureResolver>();
        services.TryAddSingleton<DetailExposureEvaluator>();
        services.TryAddSingleton<ProblemResponseWriter>();
        services.TryAddSingleton<IFaultRequestAccessor, HttpFaultRequestAccessor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFaultProvider, BadRequestFaultProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<ApiBehaviorOptions>, ApiBehaviorSetup>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<MvcOptions>, MvcSetup>());
    }
}
