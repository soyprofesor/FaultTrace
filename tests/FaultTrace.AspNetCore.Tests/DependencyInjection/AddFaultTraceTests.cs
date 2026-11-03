using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Configuration;
using FaultTrace.AspNetCore.DependencyInjection;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.DependencyInjection;
using FaultTrace.Oracle.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using Driver = global::Oracle.ManagedDataAccess.Client;

namespace FaultTrace.AspNetCore.Tests.DependencyInjection;

public class AddFaultTraceTests
{
    [Fact]
    public void AddFaultTrace_ChainsAddOracle()
    {
        var provider = new ServiceCollection().AddLogging().AddFaultTrace().AddOracle().Services.BuildServiceProvider();

        provider.GetServices<IFaultProvider>().Count(item => item.GetType().Name == "OracleFaultTranslator").ShouldBe(1);
        provider.GetRequiredService<IFaultTranslator>().ShouldNotBeNull();
        provider.GetRequiredService<IFaultRequestAccessor>().GetCurrent().ShouldBeNull();
    }

    [Fact]
    public async Task UseFaultTrace_WithoutAddFaultTrace_Throws()
    {
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => TestApp.StartAsync(
            _ => Task.CompletedTask,
            addFaultTrace: _ => { }));

        failure.Message.ShouldContain("AddFaultTrace");
    }

    [Fact]
    public async Task UseFaultTrace_AfterOnlyAddFaultTraceCore_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => TestApp.StartAsync(
            _ => Task.CompletedTask,
            addFaultTrace: services => services.AddFaultTraceCore()));
    }

    [Fact]
    public async Task AddFaultTrace_FromSection_ReadsEverySetting()
    {
        var section = CreateSection(new Dictionary<string, string?>
        {
            ["FaultTrace:DefaultCulture"] = "ru",
            ["FaultTrace:DetailExposure"] = "Always",
            ["FaultTrace:TypeBaseUri"] = "https://errors.example.test/",
            ["FaultTrace:RetryAfterSeconds"] = "9",
            ["FaultTrace:UserIdClaim"] = "employee_id",
            ["FaultTrace:Culture:Mode"] = "Sources",
            ["FaultTrace:Culture:QueryNames:0"] = "language",
            ["FaultTrace:Culture:UseAcceptLanguage"] = "false",
        });

        using var app = await TestApp.StartAsync(
            _ => throw new Driver.OracleException("ORA-03113: end-of-file on communication channel", 3113),
            addFaultTrace: services => services.AddFaultTrace(section).AddOracle());

        var problem = await app.GetProblemAsync("/x");

        problem.Get("type").ShouldBe("https://errors.example.test/unavailable");
        problem.Get("title").ShouldBe("Сервис временно недоступен");
        problem.Has("debug").ShouldBeTrue();
        problem.Headers["Retry-After"].ShouldBe("9");

        (await app.GetProblemAsync("/x?language=en")).Get("title").ShouldBe("Service unavailable");
        (await app.GetProblemAsync("/x?culture=en")).Get("title").ShouldBe("Сервис временно недоступен");

        var options = app.Services.GetRequiredService<IOptionsMonitor<FaultTraceAspNetCoreOptions>>().CurrentValue;
        options.UserIdClaim.ShouldBe("employee_id");
        options.Culture.QueryNames.ShouldBe(new[] { "language" });
    }

    [Fact]
    public async Task AddFaultTrace_FromSection_ReloadsWithoutRestart()
    {
        var source = new MemoryConfigurationSource
        {
            InitialData = new Dictionary<string, string?> { ["FaultTrace:DetailExposure"] = "Never" }!,
        };
        var root = new ConfigurationBuilder().Add(source).Build();

        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            addFaultTrace: services => services.AddFaultTrace(root.GetSection("FaultTrace")));

        (await app.GetProblemAsync()).Has("debug").ShouldBeFalse();

        root["FaultTrace:DetailExposure"] = "Always";
        root.Reload();

        (await app.GetProblemAsync()).Has("debug").ShouldBeTrue();
    }

    [Fact]
    public async Task AddFaultTrace_FromSection_AcceptsDelegatesToo()
    {
        var section = CreateSection(new Dictionary<string, string?> { ["FaultTrace:DetailExposure"] = "Always" });

        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            addFaultTrace: services => services.AddFaultTrace(section, options =>
            {
                options.DetailExposurePredicate = context => context.Request.Query.ContainsKey("dev");
                options.AddCultureAlias("oz", "ru");
            }));

        (await app.GetProblemAsync("/x")).Has("debug").ShouldBeFalse();

        var problem = await app.GetProblemAsync("/x?dev=1&culture=oz");
        problem.Has("debug").ShouldBeTrue();
        problem.Get("title").ShouldBe("Непредвиденная ошибка");
    }

    private static IConfigurationSection CreateSection(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values!).Build().GetSection("FaultTrace");
    }
}
