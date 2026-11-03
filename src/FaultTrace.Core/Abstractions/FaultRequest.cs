namespace FaultTrace.Core.Abstractions;

/// <summary>
/// The request a fault happened in, as seen by <see cref="IFaultRequestAccessor"/>.
/// </summary>
public sealed class FaultRequest
{
    /// <summary>
    /// Creates a request description.
    /// </summary>
    /// <param name="endpoint">What was called, for example a URL path.</param>
    /// <param name="method">How it was called, for example an HTTP method.</param>
    /// <param name="user">The user ID.</param>
    /// <param name="traceId">The trace ID to use when there is no current <see cref="System.Diagnostics.Activity"/>.</param>
    /// <param name="isAborted">Whether the caller has abandoned the request.</param>
    public FaultRequest(string? endpoint, string? method, string? user, string? traceId = null, bool isAborted = false)
    {
        Endpoint = endpoint;
        Method = method;
        User = user;
        TraceId = traceId;
        IsAborted = isAborted;
    }

    /// <summary>What was called, or <see langword="null"/> if unknown.</summary>
    public string? Endpoint { get; }

    /// <summary>How it was called, or <see langword="null"/> if unknown.</summary>
    public string? Method { get; }

    /// <summary>The user ID, or <see langword="null"/> if unknown.</summary>
    public string? User { get; }

    /// <summary>The trace ID to use when there is no current <see cref="System.Diagnostics.Activity"/>.</summary>
    public string? TraceId { get; }

    /// <summary>Whether the caller has abandoned the request. A timeout then counts as the client closing it.</summary>
    public bool IsAborted { get; }
}
