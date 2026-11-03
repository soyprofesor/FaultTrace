using FaultTrace.Core.Model;
using FaultTrace.Oracle.Classification;
using Shouldly;
using Xunit;

namespace FaultTrace.Oracle.Tests.Classification;

public class OracleErrorClassifierTests
{
    [Theory]
    [InlineData(20000, "business", 400, FaultCategory.Business)]
    [InlineData(20999, "business", 400, FaultCategory.Business)]
    [InlineData(1400, "required_field", 400, FaultCategory.Validation)]
    [InlineData(1407, "required_field", 400, FaultCategory.Validation)]
    [InlineData(12899, "value_too_long", 400, FaultCategory.Validation)]
    [InlineData(1401, "value_too_long", 400, FaultCategory.Validation)]
    [InlineData(1722, "invalid_format", 400, FaultCategory.Validation)]
    [InlineData(1438, "invalid_format", 400, FaultCategory.Validation)]
    [InlineData(1830, "invalid_format", 400, FaultCategory.Validation)]
    [InlineData(1861, "invalid_format", 400, FaultCategory.Validation)]
    [InlineData(1840, "invalid_format", 400, FaultCategory.Validation)]
    [InlineData(2291, "reference_not_found", 400, FaultCategory.Validation)]
    [InlineData(1, "duplicate", 409, FaultCategory.Conflict)]
    [InlineData(2292, "has_dependents", 409, FaultCategory.Conflict)]
    [InlineData(1403, "unexpected", 500, FaultCategory.Internal)]
    [InlineData(1422, "unexpected", 500, FaultCategory.Internal)]
    [InlineData(6502, "unexpected", 500, FaultCategory.Internal)]
    [InlineData(6550, "unexpected", 500, FaultCategory.Internal)]
    [InlineData(942, "unexpected", 500, FaultCategory.Internal)]
    [InlineData(1031, "unexpected", 500, FaultCategory.Internal)]
    [InlineData(4061, "unexpected", 500, FaultCategory.Internal)]
    [InlineData(6508, "unexpected", 500, FaultCategory.Internal)]
    [InlineData(60, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(54, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(4068, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(3113, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(3114, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(3135, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(12000, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(12541, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(50000, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(50012, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(50201, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(-1000, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(-1012, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(-6001, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(12999, "unavailable", 503, FaultCategory.Transient)]
    [InlineData(1013, "timeout", 504, FaultCategory.Timeout)]
    public void Classify_MappedCodes(int number, string code, int status, FaultCategory category)
    {
        var result = OracleErrorClassifier.Classify(number);

        result.Code.ShouldBe(code);
        result.Status.ShouldBe(status);
        result.Category.ShouldBe(category);
        result.IsMapped.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(19999)]
    [InlineData(21000)]
    [InlineData(11999)]
    [InlineData(13000)]
    [InlineData(59999)]
    public void Classify_UnknownCodes_AreUnexpectedAndUnmapped(int? number)
    {
        var result = OracleErrorClassifier.Classify(number);

        result.Code.ShouldBe(FaultCodes.Unexpected);
        result.Status.ShouldBe(500);
        result.Category.ShouldBe(FaultCategory.Internal);
        result.IsTransient.ShouldBeFalse();
        result.IsMapped.ShouldBeFalse();
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(12541, true)]
    [InlineData(50201, true)]
    [InlineData(1013, true)]
    [InlineData(12899, false)]
    [InlineData(1400, false)]
    [InlineData(20001, false)]
    public void Classify_SetsIsTransient(int number, bool expected)
    {
        OracleErrorClassifier.Classify(number).IsTransient.ShouldBe(expected);
    }
}
