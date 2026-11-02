using Microsoft.Extensions.DependencyInjection;

namespace FaultTrace.Core.DependencyInjection;

/// <summary>
/// Continues FaultTrace registration, for example to add a provider.
/// </summary>
public interface IFaultTraceBuilder
{
    /// <summary>The services being configured.</summary>
    IServiceCollection Services { get; }
}
