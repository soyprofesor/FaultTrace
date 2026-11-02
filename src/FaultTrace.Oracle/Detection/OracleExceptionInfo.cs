using System;

namespace FaultTrace.Oracle.Detection;

internal sealed class OracleExceptionInfo
{
    public OracleExceptionInfo(Exception exception, int? number, string message)
    {
        Exception = exception;
        Number = number;
        Message = message;
    }

    public Exception Exception { get; }

    // Null when the property couldn't be read. Callers then rely on the parsed message.
    public int? Number { get; }

    public string Message { get; }
}
