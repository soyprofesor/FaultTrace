using System;
using System.Collections.Generic;
using FaultTrace.Core.Model;

namespace FaultTrace.Oracle.Parsing;

internal sealed class ParsedOracleError
{
    public static readonly ParsedOracleError Empty = new();

    public static readonly ParsedOracleError Failed = new(parseFailed: true);

    public ParsedOracleError(
        int? number = null,
        string? nativeCode = null,
        string? businessMessage = null,
        FaultFrame? origin = null,
        FaultFrame? entry = null,
        IReadOnlyList<FaultSegment>? chain = null,
        IReadOnlyDictionary<string, string>? details = null,
        bool parseFailed = false)
    {
        Number = number;
        NativeCode = nativeCode;
        BusinessMessage = businessMessage;
        Origin = origin;
        Entry = entry;
        Chain = chain ?? [];
        Details = details ?? new Dictionary<string, string>();
        ParseFailed = parseFailed;
    }

    // The code of the first error. This is what the error is classified by.
    public int? Number { get; }

    public string? NativeCode { get; }

    // Set only when Number is a business error. The developer's text without any ORA codes.
    public string? BusinessMessage { get; }

    public FaultFrame? Origin { get; }

    public FaultFrame? Entry { get; }

    public IReadOnlyList<FaultSegment> Chain { get; }

    public IReadOnlyDictionary<string, string> Details { get; }

    public bool ParseFailed { get; }
}
