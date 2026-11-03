namespace FaultTrace.Core.Model;

/// <summary>
/// Stable machine keys for <see cref="Fault.Code"/>. Clients rely on these values, so they never change.
/// </summary>
public static class FaultCodes
{
    /// <summary>A business rule rejected the operation.</summary>
    public const string Business = "business";

    /// <summary>The input failed validation.</summary>
    public const string Validation = "validation";

    /// <summary>A required value is missing.</summary>
    public const string RequiredField = "required_field";

    /// <summary>A value is longer than allowed.</summary>
    public const string ValueTooLong = "value_too_long";

    /// <summary>A value has an invalid format.</summary>
    public const string InvalidFormat = "invalid_format";

    /// <summary>A referenced record does not exist.</summary>
    public const string ReferenceNotFound = "reference_not_found";

    /// <summary>The caller is not authenticated.</summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>The caller is not allowed to perform the operation.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>The requested resource does not exist.</summary>
    public const string NotFound = "not_found";

    /// <summary>The resource does not support the request method.</summary>
    public const string MethodNotAllowed = "method_not_allowed";

    /// <summary>The request content type is not supported.</summary>
    public const string UnsupportedMediaType = "unsupported_media_type";

    /// <summary>The record already exists.</summary>
    public const string Duplicate = "duplicate";

    /// <summary>The record cannot be removed because other records depend on it.</summary>
    public const string HasDependents = "has_dependents";

    /// <summary>An unexpected failure, usually a bug.</summary>
    public const string Unexpected = "unexpected";

    /// <summary>A required service is temporarily unavailable.</summary>
    public const string Unavailable = "unavailable";

    /// <summary>The operation did not finish in time.</summary>
    public const string Timeout = "timeout";

    /// <summary>The client aborted the request.</summary>
    public const string ClientClosed = "client_closed";
}
