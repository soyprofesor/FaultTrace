using System;
using System.Collections.Generic;
using System.Linq;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.DependencyInjection;
using FaultTrace.Core.Model;
using FaultTrace.Oracle.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Driver = global::Oracle.ManagedDataAccess.Client;

namespace FaultTrace.AspNetCore.Tests.DependencyInjection;

// Lives here because this project has a real DI container through the ASP.NET Core shared framework.
public class AddFaultTraceCoreTests
{
    private const string RequiredFieldMessage =
        "ORA-01400: cannot insert NULL into (\"APP\".\"ORDERS\".\"TREASURY_CODE\")\n" +
        "ORA-06512: at \"APP.ORDER_PACK\", line 775\n" +
        "ORA-06512: at line 1";

    [Fact]
    public void Translator_WorksWithoutLoggingRegistered()
    {
        var translator = new ServiceCollection()
            .AddFaultTraceCore()
            .Services
            .BuildServiceProvider()
            .GetRequiredService<IFaultTranslator>();

        translator.Translate(new InvalidOperationException()).Code.ShouldBe(FaultCodes.Unexpected);
    }

    [Fact]
    public void Options_ChangeMessages()
    {
        var translator = new ServiceCollection()
            .AddLogging()
            .AddFaultTraceCore(options => options.DefaultCulture = "uz")
            .Services
            .BuildServiceProvider()
            .GetRequiredService<IFaultTranslator>();

        translator.Translate(new InvalidOperationException()).UserMessage.ShouldStartWith("Kutilmagan xatolik");
    }

    [Fact]
    public void AddOracle_TranslatesOracleExceptionsEndToEnd()
    {
        var translator = new ServiceCollection()
            .AddLogging()
            .AddFaultTraceCore()
            .AddOracle(oracle => oracle.MapColumn("TREASURY_CODE", new Dictionary<string, string> { ["en"] = "Treasury code" }))
            .Services
            .BuildServiceProvider()
            .GetRequiredService<IFaultTranslator>();

        var exception = new InvalidOperationException("wrapper", new Driver.OracleException(RequiredFieldMessage, 1400));

        var fault = translator.Translate(exception);

        fault.Code.ShouldBe(FaultCodes.RequiredField);
        fault.Status.ShouldBe(400);
        fault.UserMessage.ShouldBe("A required field is missing: Treasury code");
        fault.Provider!.Origin!.ToString().ShouldBe("APP.ORDER_PACK:775");
    }

    [Fact]
    public void AddOracle_CalledTwice_RegistersOneProvider()
    {
        var services = new ServiceCollection()
            .AddFaultTraceCore()
            .AddOracle()
            .AddOracle()
            .Services
            .BuildServiceProvider();

        services.GetServices<IFaultProvider>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddOracle_NonOracleException_FallsThroughToUnexpected()
    {
        var translator = new ServiceCollection()
            .AddFaultTraceCore()
            .AddOracle()
            .Services
            .BuildServiceProvider()
            .GetRequiredService<IFaultTranslator>();

        translator.Translate(new ArgumentException("x")).Code.ShouldBe(FaultCodes.Unexpected);
    }
}
