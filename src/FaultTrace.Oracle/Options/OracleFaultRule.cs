using System;
using System.Collections.Generic;
using FaultTrace.Oracle.Classification;

namespace FaultTrace.Oracle.Options;

internal sealed class OracleFaultRule
{
    public OracleFaultRule(
        Func<OracleErrorInfo, bool> predicate,
        OracleClassification classification,
        Dictionary<string, string>? messages)
    {
        Predicate = predicate;
        Classification = classification;
        Messages = messages;
    }

    public Func<OracleErrorInfo, bool> Predicate { get; }

    public OracleClassification Classification { get; }

    public Dictionary<string, string>? Messages { get; }
}
