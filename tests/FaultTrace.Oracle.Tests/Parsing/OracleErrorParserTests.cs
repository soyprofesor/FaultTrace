using System;
using System.IO;
using System.Linq;
using FaultTrace.Oracle.Parsing;
using Shouldly;
using Xunit;

namespace FaultTrace.Oracle.Tests.Parsing;

public class OracleErrorParserTests
{
    [Fact]
    public void SingleBusiness_PassesTextThroughWithoutStack()
    {
        var parsed = Parse("single-business.txt");

        parsed.Number.ShouldBe(20001);
        parsed.NativeCode.ShouldBe("ORA-20001");
        parsed.BusinessMessage.ShouldBe("Order amount must be positive");
        parsed.Origin!.ToString().ShouldBe("APP.ORDER_PACK:120");
        parsed.Entry!.ToString().ShouldBe("APP.ORDER_PACK:120");
    }

    [Fact]
    public void TechnicalWithStack_FindsOriginEntryAndDetails()
    {
        var parsed = Parse("technical-with-stack.txt");

        parsed.Number.ShouldBe(1400);
        parsed.BusinessMessage.ShouldBeNull();
        parsed.Origin!.ToString().ShouldBe("APP.ORDER_REQ:1749");
        parsed.Entry!.ToString().ShouldBe("APP.ORDER_PACK:775");
        parsed.Chain.Count.ShouldBe(1);
        parsed.Chain[0].Frames.Select(frame => frame.ToString()).ShouldBe(new[]
        {
            "APP.ORDER_REQ:1749",
            "APP.ORDER_PACK:775",
            "anonymous:1",
        });
        parsed.Details[OracleDetailKeys.Schema].ShouldBe("APP");
        parsed.Details[OracleDetailKeys.Table].ShouldBe("ORDERS");
        parsed.Details[OracleDetailKeys.Column].ShouldBe("TREASURY_CODE");
    }

    [Fact]
    public void NestedRaise_KeepsSegmentsAndTakesOriginFromFirst()
    {
        var parsed = Parse("nested-raise.txt");

        parsed.Chain.Count.ShouldBe(2);
        parsed.Chain[0].Frames.Single().ToString().ShouldBe("APP.ORDER_REQ:1749");
        parsed.Chain[1].Frames.Count.ShouldBe(2);
        parsed.Origin!.ToString().ShouldBe("APP.ORDER_REQ:1749");
        parsed.Entry!.ToString().ShouldBe("APP.ORDER_PACK:780");
    }

    [Fact]
    public void BusinessFromATrigger_TakesOriginFromTheTrigger()
    {
        var parsed = Parse("trigger-business.txt");

        parsed.BusinessMessage.ShouldBe("Row is locked");
        parsed.Origin!.ToString().ShouldBe("APP.ORDERS_TRG:2");
        parsed.Entry!.ToString().ShouldBe("APP.ORDER_PACK:187");
    }

    [Fact]
    public void TechnicalFromATrigger_TakesOriginFromTheTrigger()
    {
        var parsed = Parse("trigger-technical.txt");

        parsed.Origin!.ToString().ShouldBe("APP.ORDERS_TRG:4");
        parsed.Entry!.ToString().ShouldBe("APP.ORDER_PACK:187");
    }

    [Fact]
    public void KeepErrorsInsideATrigger_TakesOriginFromTheTechnicalError()
    {
        var parsed = Parse("trigger-keep-errors.txt");

        parsed.Origin!.ToString().ShouldBe("APP.ORDERS_TRG:4");
    }

    [Fact]
    public void DuplicatedPrefix_IsStripped()
    {
        var parsed = Parse("duplicated-prefix.txt");

        parsed.Number.ShouldBe(20001);
        parsed.BusinessMessage.ShouldBe("Insufficient balance");
    }

    [Fact]
    public void MultilineBusiness_KeepsAllTextLines()
    {
        var parsed = Parse("multiline-business.txt");

        parsed.BusinessMessage.ShouldBe("Payment was rejected.\nContact your manager.");
    }

    [Theory]
    [InlineData("keep-errors-anonymous-on-top.txt", "APP.ORDER_PACK:790")]
    [InlineData("keep-errors-anonymous-at-bottom.txt", "APP.ORDER_PACK:775")]
    public void KeepErrorsTrue_TakesOriginFromTheOriginalError(string fixture, string expectedEntry)
    {
        var parsed = Parse(fixture);

        parsed.Number.ShouldBe(20005);
        parsed.BusinessMessage.ShouldBe("Could not save the order");
        parsed.Chain.Select(segment => segment.NativeCode).ShouldBe(new[] { "ORA-20005", "ORA-01400" });
        parsed.Origin!.ToString().ShouldBe("APP.ORDER_REQ:1749");
        parsed.Entry!.ToString().ShouldBe(expectedEntry);
        parsed.Details[OracleDetailKeys.Column].ShouldBe("TREASURY_CODE");
    }

    [Fact]
    public void ErrorPackHelper_WithoutIgnoring_IsTheOrigin()
    {
        var parsed = Parse("error-pack-helper.txt");

        parsed.Origin!.ToString().ShouldBe("CORE.ERROR_PACK:15");
    }

    [Theory]
    [InlineData("ERROR_PACK")]
    [InlineData("core.error_pack")]
    public void ErrorPackHelper_IgnoredPackage_IsHiddenFromOriginAndChain(string ignored)
    {
        var parsed = OracleErrorParser.Parse(Fixture("error-pack-helper.txt"), new[] { ignored });

        parsed.BusinessMessage.ShouldBe("Customer is blocked");
        parsed.Origin!.ToString().ShouldBe("APP.ORDER_PACK:300");
        parsed.Entry!.ToString().ShouldBe("APP.ORDER_PACK:300");
        parsed.Chain[0].Frames.Select(frame => frame.ToString()).ShouldBe(new[]
        {
            "APP.ORDER_PACK:300",
            "anonymous:1",
        });
    }

    [Fact]
    public void IgnoredPackage_MatchesWholeNamesOnly()
    {
        var parsed = OracleErrorParser.Parse(Fixture("error-pack-helper.txt"), new[] { "PACK" });

        parsed.Origin!.ToString().ShouldBe("CORE.ERROR_PACK:15");
    }

    [Fact]
    public void HelpUrl_IsRemovedFromTechnicalSegments()
    {
        var parsed = Parse("help-url-technical.txt");

        parsed.Number.ShouldBe(1);
        parsed.Chain[0].Message!.ShouldNotContain("docs.oracle.com");
        parsed.Details[OracleDetailKeys.Schema].ShouldBe("APP");
        parsed.Details[OracleDetailKeys.Constraint].ShouldBe("UQ_ORDERS_CODE");
    }

    [Fact]
    public void HelpUrl_IsRemovedFromBusinessMessage()
    {
        var parsed = Parse("help-url-business.txt");

        parsed.BusinessMessage.ShouldBe("Contract has expired");
    }

    [Fact]
    public void RussianMessage_IsParsedTheSameWay()
    {
        var parsed = Parse("russian-technical.txt");

        parsed.Number.ShouldBe(1400);
        parsed.Origin!.ToString().ShouldBe("APP.ORDER_REQ:1749");
        parsed.Entry!.ToString().ShouldBe("APP.ORDER_PACK:775");
        parsed.Chain[0].Frames.Last().Unit.ShouldBeNull();
        parsed.Details[OracleDetailKeys.Column].ShouldBe("TREASURY_CODE");
    }

    [Fact]
    public void LabelledBacktrace_RecoversOriginAndHidesLabel()
    {
        var parsed = Parse("backtrace-labelled.txt");

        parsed.BusinessMessage.ShouldBe("Failed to save the order");
        parsed.Origin!.ToString().ShouldBe("APP.ORDER_REQ:1749");
        parsed.Entry!.ToString().ShouldBe("APP.ORDER_PACK:790");
    }

    [Fact]
    public void UnlabelledBacktrace_IsFoundStructurally()
    {
        var parsed = Parse("backtrace-unlabelled.txt");

        parsed.Origin!.ToString().ShouldBe("APP.ORDER_REQ:1749");
        parsed.Entry!.ToString().ShouldBe("APP.ORDER_PACK:790");
    }

    [Fact]
    public void InlineBacktrace_NeverReachesTheBusinessMessage()
    {
        var parsed = Parse("backtrace-inline.txt");

        parsed.BusinessMessage.ShouldBe("Failed to save the order");
        parsed.Origin!.ToString().ShouldBe("APP.ORDER_REQ:1749");
    }

    [Theory]
    [InlineData("connection-no-listener.txt", 12541)]
    [InlineData("connection-lost.txt", 3113)]
    public void ConnectionErrors_HaveCodeAndNoFrames(string fixture, int expectedNumber)
    {
        var parsed = Parse(fixture);

        parsed.Number.ShouldBe(expectedNumber);
        parsed.Origin.ShouldBeNull();
        parsed.Entry.ShouldBeNull();
    }

    [Fact]
    public void TextStartingWithTechnicalCode_StaysBusinessWithCodeCutOut()
    {
        var parsed = Parse("wrapped-technical.txt");

        parsed.Number.ShouldBe(20001);
        parsed.NativeCode.ShouldBe("ORA-20001");
        parsed.BusinessMessage.ShouldBe("cannot insert NULL into (\"APP\".\"ORDERS\".\"TREASURY_CODE\")");
    }

    [Fact]
    public void TextContainingTechnicalCode_StaysBusinessWithCodeCutOut()
    {
        var parsed = Parse("embedded-technical.txt");

        parsed.Number.ShouldBe(20001);
        parsed.BusinessMessage.ShouldBe("Could not save the order: cannot insert NULL into (\"APP\".\"ORDERS\".\"TREASURY_CODE\")");
        parsed.Chain[0].Message!.ShouldContain("ORA-01400");
    }

    [Fact]
    public void BusinessCodeInsideText_IsCutOut()
    {
        var parsed = Parse("embedded-business.txt");

        parsed.Number.ShouldBe(20001);
        parsed.BusinessMessage.ShouldBe("Could not save the order: Limit exceeded");
    }

    [Fact]
    public void ValueTooLong_ExtractsColumnWithoutParentheses()
    {
        var parsed = Parse("value-too-long.txt");

        parsed.Number.ShouldBe(12899);
        parsed.Details[OracleDetailKeys.Table].ShouldBe("ORDERS");
        parsed.Details[OracleDetailKeys.Column].ShouldBe("CODE");
    }

    [Fact]
    public void BusinessText_IsNotReadAsDetails()
    {
        var parsed = OracleErrorParser.Parse("ORA-20001: Limit exceeded (A.B)");

        parsed.Details.ShouldBeEmpty();
    }

    [Fact]
    public void WindowsLineEndings_AreHandled()
    {
        var parsed = OracleErrorParser.Parse(Fixture("technical-with-stack.txt").Replace("\n", "\r\n"));

        parsed.Chain[0].Frames.Count.ShouldBe(3);
        parsed.Origin!.ToString().ShouldBe("APP.ORDER_REQ:1749");
    }

    [Fact]
    public void BusinessWithOnlyPrefix_HasNoMessage()
    {
        OracleErrorParser.Parse("ORA-20001: ").BusinessMessage.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Connection request timed out")]
    [InlineData("ORA-")]
    [InlineData("ORA-1234: too short")]
    public void TextWithoutOracleErrors_GivesEmptyResult(string? message)
    {
        var parsed = OracleErrorParser.Parse(message);

        parsed.Number.ShouldBeNull();
        parsed.Chain.ShouldBeEmpty();
        parsed.Origin.ShouldBeNull();
        parsed.ParseFailed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("ORA-06512: at \"APP.X\", line 5")]
    [InlineData("ORA-06512:")]
    [InlineData("ORA-06512: at \"unterminated, line 5")]
    public void FramesWithoutError_DoNotThrow(string message)
    {
        var parsed = OracleErrorParser.Parse(message);

        parsed.Number.ShouldBeNull();
        parsed.ParseFailed.ShouldBeFalse();
    }

    [Fact]
    public void VeryLongInput_DoesNotThrow()
    {
        var message = "ORA-20001: " + new string('"', 200_000) + "\nORA-06512: at " + new string('"', 200_000);

        Should.NotThrow(() => OracleErrorParser.Parse(message));
    }

    private static ParsedOracleError Parse(string fixture) => OracleErrorParser.Parse(Fixture(fixture));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
