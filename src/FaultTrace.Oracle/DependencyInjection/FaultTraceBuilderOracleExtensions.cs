using System;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.DependencyInjection;
using FaultTrace.Oracle.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FaultTrace.Oracle.DependencyInjection;

/// <summary>
/// Adds the Oracle provider to FaultTrace.
/// </summary>
public static class FaultTraceBuilderOracleExtensions
{
    /// <summary>
    /// Adds the Oracle provider.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Changes the settings.</param>
    /// <returns>The same builder.</returns>
    public static IFaultTraceBuilder AddOracle(this IFaultTraceBuilder builder, Action<OracleFaultOptions>? configure = null)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (configure != null)
        {
            builder.Services.Configure(configure);
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IFaultProvider, OracleFaultTranslator>());

        return builder;
    }
}
