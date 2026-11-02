using FaultTrace.Core.Model;

namespace FaultTrace.Oracle.Classification;

internal sealed class OracleClassification
{
    public OracleClassification(FaultCategory category, string code, int status, bool isTransient, bool isMapped)
    {
        Category = category;
        Code = code;
        Status = status;
        IsTransient = isTransient;
        IsMapped = isMapped;
    }

    public FaultCategory Category { get; }

    public string Code { get; }

    public int Status { get; }

    public bool IsTransient { get; }

    // False for unknown codes, so they can be logged and added to the catalog later.
    public bool IsMapped { get; }
}
