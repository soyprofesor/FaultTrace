using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FaultTrace.Core.Model;

/// <summary>
/// Provider-specific details of a <see cref="Fault"/>.
/// For logs and debugging only: never put these into a production response.
/// </summary>
public sealed class ProviderFaultDetails
{
    /// <summary>
    /// Creates provider details.
    /// </summary>
    /// <param name="provider">The provider name, for example <c>Oracle</c>.</param>
    /// <param name="nativeCode">The provider's own error code, for example <c>ORA-01400</c>.</param>
    /// <param name="origin">Where the error first happened.</param>
    /// <param name="entry">What the application called.</param>
    /// <param name="chain">The error segments, with re-raise boundaries preserved.</param>
    /// <param name="details">Named values extracted from the error, for example a table or column name.</param>
    /// <param name="rawMessage">The complete original message.</param>
    /// <exception cref="ArgumentException"><paramref name="provider"/> is empty.</exception>
    public ProviderFaultDetails(
        string provider,
        string? nativeCode = null,
        FaultFrame? origin = null,
        FaultFrame? entry = null,
        IEnumerable<FaultSegment>? chain = null,
        IReadOnlyDictionary<string, string>? details = null,
        string? rawMessage = null)
    {
        Provider = Guard.NotEmpty(provider, nameof(provider));
        NativeCode = nativeCode;
        Origin = origin;
        Entry = entry;
        Chain = Array.AsReadOnly(chain?.ToArray() ?? Array.Empty<FaultSegment>());
        Details = new ReadOnlyDictionary<string, string>(
            details?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? new Dictionary<string, string>());
        RawMessage = rawMessage;
    }

    /// <summary>The provider name, for example <c>Oracle</c>.</summary>
    public string Provider { get; }

    /// <summary>The provider's own error code, or <see langword="null"/> if unknown.</summary>
    public string? NativeCode { get; }

    /// <summary>Where the error first happened, or <see langword="null"/> if unknown.</summary>
    public FaultFrame? Origin { get; }

    /// <summary>What the application called, or <see langword="null"/> if unknown.</summary>
    public FaultFrame? Entry { get; }

    /// <summary>The error segments, with re-raise boundaries preserved. Never <see langword="null"/>.</summary>
    public IReadOnlyList<FaultSegment> Chain { get; }

    /// <summary>Named values extracted from the error. Never <see langword="null"/>.</summary>
    public IReadOnlyDictionary<string, string> Details { get; }

    /// <summary>The complete original message, or <see langword="null"/> if unknown.</summary>
    public string? RawMessage { get; }
}
