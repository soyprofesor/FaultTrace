using System;
using FaultTrace.Core.Model;

namespace FaultTrace.Core.Abstractions;

/// <summary>
/// The values an <see cref="IFaultProvider"/> needs to build a <see cref="Fault"/>.
/// </summary>
public sealed class FaultContext
{
    /// <summary>
    /// Creates a context.
    /// </summary>
    /// <param name="errorId">The error ID the fault must carry.</param>
    /// <param name="traceId">The trace ID, if there is one.</param>
    /// <param name="culture">The culture for library-generated messages, or <see langword="null"/> for the default culture.</param>
    /// <exception cref="ArgumentException"><paramref name="errorId"/> is empty.</exception>
    public FaultContext(string errorId, string? traceId = null, string? culture = null)
    {
        ErrorId = Guard.NotEmpty(errorId, nameof(errorId));
        TraceId = traceId;
        Culture = culture;
    }

    /// <summary>The error ID the fault must carry.</summary>
    public string ErrorId { get; }

    /// <summary>The trace ID, or <see langword="null"/> if there is none.</summary>
    public string? TraceId { get; }

    /// <summary>The culture for library-generated messages, or <see langword="null"/> for the default culture.</summary>
    public string? Culture { get; }
}
