namespace FaultTrace.Core.Model;

/// <summary>
/// The broad kind of a <see cref="Fault"/>. Drives log level and retry decisions.
/// </summary>
public enum FaultCategory
{
    /// <summary>A business rule rejected the operation. The message is meant for the user.</summary>
    Business,

    /// <summary>The input is invalid or incomplete.</summary>
    Validation,

    /// <summary>The caller is not authenticated.</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but not allowed to perform the operation.</summary>
    Forbidden,

    /// <summary>The requested resource does not exist.</summary>
    NotFound,

    /// <summary>The operation conflicts with the current state of the data.</summary>
    Conflict,

    /// <summary>A temporary failure. Retrying may succeed.</summary>
    Transient,

    /// <summary>The operation did not finish in time.</summary>
    Timeout,

    /// <summary>The client aborted the request.</summary>
    ClientClosed,

    /// <summary>An unexpected failure, usually a bug.</summary>
    Internal,
}
