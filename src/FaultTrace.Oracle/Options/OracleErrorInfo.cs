using System.Collections.Generic;

namespace FaultTrace.Oracle.Options;

/// <summary>
/// What a custom rule can see about an Oracle error.
/// </summary>
public sealed class OracleErrorInfo
{
    internal OracleErrorInfo(int? number, string? origin, string? entry, IReadOnlyDictionary<string, string> details, string rawMessage)
    {
        Number = number;
        Origin = origin;
        Entry = entry;
        Details = details;
        RawMessage = rawMessage;
    }

    /// <summary>The error number, for example <c>1400</c>, or <see langword="null"/> if unknown.</summary>
    public int? Number { get; }

    /// <summary>Where the error first happened, as <c>UNIT:LINE</c>, or <see langword="null"/> if unknown.</summary>
    public string? Origin { get; }

    /// <summary>What the application called, as <c>UNIT:LINE</c>, or <see langword="null"/> if unknown.</summary>
    public string? Entry { get; }

    /// <summary>The schema, table, column or constraint found in the message.</summary>
    public IReadOnlyDictionary<string, string> Details { get; }

    /// <summary>The complete original message.</summary>
    public string RawMessage { get; }
}
