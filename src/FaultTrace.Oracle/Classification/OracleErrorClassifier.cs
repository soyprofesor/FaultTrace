using System.Collections.Generic;
using FaultTrace.Core.Model;

namespace FaultTrace.Oracle.Classification;

// Classifies by code number only, never by message text.
internal static class OracleErrorClassifier
{
    private static readonly OracleClassification Business = Mapped(FaultCategory.Business, FaultCodes.Business, 400);
    private static readonly OracleClassification Unavailable = Mapped(FaultCategory.Transient, FaultCodes.Unavailable, 503, isTransient: true);
    private static readonly OracleClassification Unmapped = new(FaultCategory.Internal, FaultCodes.Unexpected, 500, isTransient: false, isMapped: false);

    private static readonly Dictionary<int, OracleClassification> ExactCodes = BuildExactCodes();

    public static bool IsBusiness(int number) => number >= 20000 && number <= 20999;

    public static OracleClassification Classify(int? number)
    {
        if (!number.HasValue)
        {
            return Unmapped;
        }

        // Exact codes come first: ORA-12899 is value_too_long, not part of the TNS range.
        if (ExactCodes.TryGetValue(number.Value, out var exact))
        {
            return exact;
        }

        if (IsBusiness(number.Value))
        {
            return Business;
        }

        return number.Value >= 12000 && number.Value <= 12999 ? Unavailable : Unmapped;
    }

    private static Dictionary<int, OracleClassification> BuildExactCodes()
    {
        var codes = new Dictionary<int, OracleClassification>();

        Add(codes, Mapped(FaultCategory.Validation, FaultCodes.RequiredField, 400), 1400, 1407);
        Add(codes, Mapped(FaultCategory.Validation, FaultCodes.ValueTooLong, 400), 12899, 1401);
        Add(codes, Mapped(FaultCategory.Validation, FaultCodes.InvalidFormat, 400), 1722, 1438, 1830, 1861, 1840);
        Add(codes, Mapped(FaultCategory.Validation, FaultCodes.ReferenceNotFound, 400), 2291);
        Add(codes, Mapped(FaultCategory.Conflict, FaultCodes.Duplicate, 409), 1);
        Add(codes, Mapped(FaultCategory.Conflict, FaultCodes.HasDependents, 409), 2292);
        Add(codes, Mapped(FaultCategory.Internal, FaultCodes.Unexpected, 500), 1403, 1422, 6502, 6550, 942, 1031, 4061, 6508);
        // 50012 and 50201 come from the driver itself, not the server: pool timeout and failed connection.
        Add(codes, Unavailable, 60, 54, 4068, 3113, 3114, 3135, 50012, 50201);
        Add(codes, Mapped(FaultCategory.Timeout, FaultCodes.Timeout, 504, isTransient: true), 1013);

        return codes;
    }

    private static void Add(Dictionary<int, OracleClassification> codes, OracleClassification classification, params int[] numbers)
    {
        foreach (var number in numbers)
        {
            codes[number] = classification;
        }
    }

    private static OracleClassification Mapped(FaultCategory category, string code, int status, bool isTransient = false) =>
        new(category, code, status, isTransient, isMapped: true);
}
