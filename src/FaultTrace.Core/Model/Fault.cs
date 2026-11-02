using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FaultTrace.Core.ErrorIds;

namespace FaultTrace.Core.Model;

/// <summary>
/// The result of translating an exception: what the user may see, plus details for the logs.
/// </summary>
public sealed class Fault
{
    /// <summary>
    /// Creates a fault.
    /// </summary>
    /// <param name="errorId">The short user-facing ID. See <see cref="ErrorIdGenerator"/>.</param>
    /// <param name="category">The broad kind of the fault.</param>
    /// <param name="code">The stable machine key. See <see cref="FaultCodes"/>.</param>
    /// <param name="userMessage">The text that is safe to show to the user.</param>
    /// <param name="status">The suggested HTTP status, from 400 to 599.</param>
    /// <param name="isTransient">Whether retrying makes sense.</param>
    /// <param name="traceId">The trace ID, if there is one.</param>
    /// <param name="errors">Field to messages. Only allowed for <see cref="FaultCategory.Validation"/>.</param>
    /// <param name="provider">Provider-specific details for logs and debugging.</param>
    /// <exception cref="ArgumentException">A required text is empty, or <paramref name="errors"/> is set on a non-validation fault.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not an error status.</exception>
    public Fault(
        string errorId,
        FaultCategory category,
        string code,
        string userMessage,
        int status,
        bool isTransient = false,
        string? traceId = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null,
        ProviderFaultDetails? provider = null)
    {
        if (status < 400 || status > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Status must be between 400 and 599.");
        }

        if (errors != null && errors.Count > 0 && category != FaultCategory.Validation)
        {
            throw new ArgumentException("Errors are only allowed on validation faults.", nameof(errors));
        }

        ErrorId = Guard.NotEmpty(errorId, nameof(errorId));
        Category = category;
        Code = Guard.NotEmpty(code, nameof(code));
        UserMessage = Guard.NotEmpty(userMessage, nameof(userMessage));
        Status = status;
        IsTransient = isTransient;
        TraceId = traceId;
        Errors = CopyErrors(errors);
        Provider = provider;
    }

    /// <summary>The short user-facing ID, in the form <c>XXXX-XXXX</c>.</summary>
    public string ErrorId { get; }

    /// <summary>The broad kind of the fault.</summary>
    public FaultCategory Category { get; }

    /// <summary>The stable machine key. See <see cref="FaultCodes"/>.</summary>
    public string Code { get; }

    /// <summary>The text that is safe to show to the user.</summary>
    public string UserMessage { get; }

    /// <summary>The suggested HTTP status.</summary>
    public int Status { get; }

    /// <summary>Whether retrying makes sense.</summary>
    public bool IsTransient { get; }

    /// <summary>The trace ID, or <see langword="null"/> if there is none.</summary>
    public string? TraceId { get; }

    /// <summary>Field to messages, or <see langword="null"/> if there are none.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors { get; }

    /// <summary>
    /// Provider-specific details, or <see langword="null"/> if there are none.
    /// For logs and debugging only: never put these into a production response.
    /// </summary>
    public ProviderFaultDetails? Provider { get; }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? CopyErrors(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? errors)
    {
        if (errors == null || errors.Count == 0)
        {
            return null;
        }

        var copy = new Dictionary<string, IReadOnlyList<string>>(errors.Count);
        foreach (var pair in errors)
        {
            copy[pair.Key] = Array.AsReadOnly(pair.Value.ToArray());
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(copy);
    }
}
