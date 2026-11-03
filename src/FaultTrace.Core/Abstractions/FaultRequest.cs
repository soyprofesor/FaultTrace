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
    /// <param name="culture">The culture the caller asked for, used when a translation doesn't name one.</param>
    public FaultRequest(string? endpoint, string? method, string? user, string? traceId = null, bool isAborted = false, string? culture = null)
    {
        Endpoint = endpoint;
        Method = method;
        User = user;
        TraceId = traceId;
        IsAborted = isAborted;
        Culture = culture;
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

    /// <summary>The culture the caller asked for, or <see langword="null"/> for the default culture.</summary>
    public string? Culture { get; }
}
