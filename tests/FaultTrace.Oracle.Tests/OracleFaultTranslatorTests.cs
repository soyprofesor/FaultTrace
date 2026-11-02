using System;
using System.Collections.Generic;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;
using FaultTrace.Core.Options;
using FaultTrace.Oracle.Options;
using Shouldly;
using Xunit;
using Driver = global::Oracle.ManagedDataAccess.Client;

namespace FaultTrace.Oracle.Tests;

public class OracleFaultTranslatorTests
{
    private const string RequiredFieldMessage =
        "ORA-01400: cannot insert NULL into (\"APP\".\"ORDERS\".\"TREASURY_CODE\")\n" +
        "ORA-06512: at \"APP.ORDER_REQ\", line 1749\n" +
        "ORA-06512: at \"CORE.ERROR_PACK\", line 15\n" +
        "ORA-06512: at \"APP.ORDER_PACK\", line 775\n" +
        "ORA-06512: at line 1";

    private const string DuplicateMessage = "ORA-00001: unique constraint (APP.UQ_ORDERS_CODE) violated";

    [Fact]
    public void TryTranslate_NonOracleException_ReturnsNull()
    {
        Create().TryTranslate(new InvalidOperationException("other"), Context()).ShouldBeNull();
    }

    [Fact]
    public void TryTranslate_Business_PassesTextThrough()
    {
        var fault = Translate("ORA-20001: Limit exceeded\nORA-06512: at \"APP.ORDER_PACK\", line 5\nORA-06512: at line 1", 20001);

        fault.Category.ShouldBe(FaultCategory.Business);
        fault.Code.ShouldBe(FaultCodes.Business);
        fault.Status.ShouldBe(400);
        fault.UserMessage.ShouldBe("Limit exceeded");
        fault.IsTransient.ShouldBeFalse();
    }

    [Fact]
    public void TryTranslate_BusinessWithoutText_UsesGenericMessage()
    {
        Translate("ORA-20001: ", 20001, "ru").UserMessage.ShouldBe("Не удалось выполнить запрос.");
    }

    [Fact]
    public void TryTranslate_CarriesContextAndProviderDetails()
    {
        var exception = new Driver.OracleException(RequiredFieldMessage, 1400);

        var fault = Create().TryTranslate(exception, new FaultContext("M3QD-7HZA", "trace", "en"))!;

        fault.ErrorId.ShouldBe("M3QD-7HZA");
        fault.TraceId.ShouldBe("trace");
        fault.Provider!.Provider.ShouldBe("Oracle");
        fault.Provider.NativeCode.ShouldBe("ORA-01400");
        fault.Provider.Origin!.ToString().ShouldBe("APP.ORDER_REQ:1749");
        fault.Provider.Entry!.ToString().ShouldBe("APP.ORDER_PACK:775");
        fault.Provider.Details["column"].ShouldBe("TREASURY_CODE");
        fault.Provider.RawMessage.ShouldBe(RequiredFieldMessage);
        fault.Provider.IsUnmapped.ShouldBeFalse();
    }

    [Fact]
    public void TryTranslate_Technical_NeverShowsDatabaseNames()
    {
        var fault = Translate(RequiredFieldMessage, 1400, "en");

        fault.Code.ShouldBe(FaultCodes.RequiredField);
        fault.UserMessage.ShouldBe("A required field is missing.");
    }

    [Fact]
    public void TryTranslate_IgnoredPackage_StaysInRawMessageOnly()
    {
        var exception = new Driver.OracleException(RequiredFieldMessage, 1400);

        var fault = Create(oracle => oracle.IgnoredPackages.Add("ERROR_PACK")).TryTranslate(exception, Context())!;

        fault.Provider!.RawMessage!.ShouldContain("ERROR_PACK");
        fault.Provider.Chain[0].Frames.ShouldNotContain(frame => frame.Unit == "CORE.ERROR_PACK");
    }

    [Theory]
    [InlineData("uz", "Majburiy maydon to'ldirilmagan: Xazina kodi")]
    [InlineData("ru", "Не заполнено обязательное поле: Код казны")]
    [InlineData("en", "A required field is missing: Treasury code")]
    public void MapColumn_AddsFriendlyNamePerCulture(string culture, string expected)
    {
        var translator = Create(oracle => oracle.MapColumn("treasury_code", new Dictionary<string, string>
        {
            ["uz"] = "Xazina kodi",
            ["ru-RU"] = "Код казны",
            ["en"] = "Treasury code",
        }));

        var resolved = CultureNormalizer.Normalize(culture);
        var fault = translator.TryTranslate(new Driver.OracleException(RequiredFieldMessage, 1400), new FaultContext("id", culture: resolved))!;

        fault.UserMessage.ShouldBe(expected);
    }

    [Fact]
    public void MapColumn_MissingCulture_FallsBackToEnglishName()
    {
        var fault = Create(oracle => oracle.MapColumn("TREASURY_CODE", new Dictionary<string, string> { ["en"] = "Treasury code" }))
            .TryTranslate(new Driver.OracleException(RequiredFieldMessage, 1400), new FaultContext("id", culture: "ru"))!;

        fault.UserMessage.ShouldBe("Не заполнено обязательное поле: Treasury code");
    }

    [Fact]
    public void MapColumn_UnmappedColumn_UsesGenericMessage()
    {
        var fault = Create(oracle => oracle.MapColumn("OTHER", new Dictionary<string, string> { ["en"] = "Other" }))
            .TryTranslate(new Driver.OracleException(RequiredFieldMessage, 1400), new FaultContext("id", culture: "en"))!;

        fault.UserMessage.ShouldBe("A required field is missing.");
    }

    [Fact]
    public void MapConstraint_ReplacesMessage()
    {
        var fault = Create(oracle => oracle.MapConstraint("UQ_ORDERS_CODE", new Dictionary<string, string> { ["ru"] = "Такой код уже есть" }))
            .TryTranslate(new Driver.OracleException(DuplicateMessage, 1), new FaultContext("id", culture: "ru"))!;

        fault.Code.ShouldBe(FaultCodes.Duplicate);
        fault.Status.ShouldBe(409);
        fault.UserMessage.ShouldBe("Такой код уже есть");
    }

    [Fact]
    public void MapConstraint_UnmappedConstraint_UsesGenericMessage()
    {
        Translate(DuplicateMessage, 1, "en").UserMessage.ShouldBe("This record already exists.");
    }

    [Fact]
    public void OverrideCode_ChangesClassification()
    {
        var fault = Create(oracle => oracle.OverrideCode(1403, FaultCategory.NotFound, FaultCodes.NotFound, 404))
            .TryTranslate(new Driver.OracleException("ORA-01403: no data found", 1403), new FaultContext("id", culture: "en"))!;

        fault.Code.ShouldBe(FaultCodes.NotFound);
        fault.Status.ShouldBe(404);
        fault.UserMessage.ShouldBe("The requested resource was not found.");
    }

    [Fact]
    public void AddRule_RunsBeforeOtherClassification()
    {
        var fault = Create(oracle => oracle.AddRule(
                error => error.Number == 1400 && error.Details["table"] == "ORDERS",
                FaultCategory.Conflict,
                "order_incomplete",
                409,
                messages: new Dictionary<string, string> { ["en"] = "The order is incomplete." }))
            .TryTranslate(new Driver.OracleException(RequiredFieldMessage, 1400), new FaultContext("id", culture: "en"))!;

        fault.Code.ShouldBe("order_incomplete");
        fault.Status.ShouldBe(409);
        fault.UserMessage.ShouldBe("The order is incomplete.");
    }

    [Fact]
    public void AddRule_ThrowingPredicate_CountsAsNoMatch()
    {
        var fault = Create(oracle => oracle.AddRule(error => throw new InvalidOperationException(), FaultCategory.Conflict, "x", 409))
            .TryTranslate(new Driver.OracleException(RequiredFieldMessage, 1400), new FaultContext("id", culture: "en"))!;

        fault.Code.ShouldBe(FaultCodes.RequiredField);
    }

    [Fact]
    public void TryTranslate_UnknownCode_IsUnexpectedAndMarkedUnmapped()
    {
        var fault = Translate("ORA-99999: something new", 99999);

        fault.Code.ShouldBe(FaultCodes.Unexpected);
        fault.Status.ShouldBe(500);
        fault.Provider!.IsUnmapped.ShouldBeTrue();
        fault.UserMessage.ShouldNotContain("something new");
    }

    [Fact]
    public void TryTranslate_Transient_IsMarked()
    {
        var fault = Translate("ORA-12541: TNS:no listener", 12541);

        fault.Code.ShouldBe(FaultCodes.Unavailable);
        fault.IsTransient.ShouldBeTrue();
        fault.Status.ShouldBe(503);
    }

    [Fact]
    public void TryTranslate_FailingDriverProperties_FallsBackToMessage()
    {
        var fault = Create().TryTranslate(new Driver.BrokenOracleException("ORA-00001: unique constraint (APP.X) violated"), Context())!;

        fault.Code.ShouldBe(FaultCodes.Duplicate);
        fault.Provider!.NativeCode.ShouldBe("ORA-00001");
    }

    [Fact]
    public void TryTranslate_MessageWithoutCode_IsUnexpected()
    {
        var fault = Create().TryTranslate(new Driver.OracleException("Connection request timed out"), Context())!;

        fault.Code.ShouldBe(FaultCodes.Unexpected);
        fault.Provider!.NativeCode.ShouldBeNull();
        fault.Provider.IsUnmapped.ShouldBeTrue();
    }

    private static Fault Translate(string message, int number, string? culture = null) =>
        Create().TryTranslate(new Driver.OracleException(message, number), new FaultContext("id", culture: culture))!;

    private static FaultContext Context() => new FaultContext("id");

    private static IFaultProvider Create(Action<OracleFaultOptions>? configure = null)
    {
        var options = new OracleFaultOptions();
        configure?.Invoke(options);

        return new OracleFaultTranslator(
            new DefaultFaultMessageProvider(Microsoft.Extensions.Options.Options.Create(new FaultTraceOptions())),
            Microsoft.Extensions.Options.Options.Create(options));
    }
}
