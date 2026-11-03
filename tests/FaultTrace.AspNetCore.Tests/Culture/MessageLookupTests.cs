using System;
using System.Globalization;
using System.Resources;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Configuration;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace FaultTrace.AspNetCore.Tests.Culture;

public class MessageLookupTests
{
    private static readonly ResourceManager Resources =
        new("FaultTrace.AspNetCore.Tests.Culture.TestMessages", typeof(MessageLookupTests).Assembly);

    [Fact]
    public async Task ResourceFile_GivesTheTextsInTheRequestedCulture()
    {
        using var app = await StartNotFoundAsync(UseResources);

        var problem = await app.GetProblemAsync("/x?culture=ru");

        problem.Get("title").ShouldBe("Заказ не найден");
        problem.Get("detail").ShouldBe("Такого заказа не существует.");
    }

    [Fact]
    public async Task ResourceFile_WorksWhateverTheThreadCultureIs()
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        try
        {
            using var app = await StartNotFoundAsync(UseResources);

            (await app.GetProblemAsync("/x?culture=ru")).Get("title").ShouldBe("Заказ не найден");
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [Fact]
    public async Task CodeMissingInTheResourceFile_UsesTheBuiltInText()
    {
        using var app = await TestApp.StartAsync(
            context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            },
            UseResources);

        (await app.GetProblemAsync("/x?culture=ru")).Get("title").ShouldBe("Доступ запрещён");
    }

    [Fact]
    public async Task Lookup_DoesNotChangeTheThreadCulture()
    {
        CultureInfo? seen = null;
        var outer = CultureInfo.CurrentUICulture;
        using var app = await StartNotFoundAsync(options => options.UseMessages((code, culture) =>
        {
            seen = CultureInfo.CurrentUICulture;
            return null;
        }));

        await app.GetProblemAsync("/x?culture=ru");

        seen.ShouldNotBeNull();
        seen!.Name.ShouldBe(outer.Name);
    }

    [Fact]
    public async Task AddedCulture_IsAcceptedFromTheRequest()
    {
        using var app = await StartNotFoundAsync(options => options
            .AddCulture("kk")
            .UseMessages((code, culture) => culture == "kk" ? new FaultMessage("Табылмады", "Тапсырыс табылмады") : null));

        var problem = await app.GetProblemAsync("/x?culture=kk");

        problem.Get("title").ShouldBe("Табылмады");
        problem.Get("detail").ShouldBe("Тапсырыс табылмады");
    }

    [Fact]
    public async Task CultureNotAdded_IsNotPassedToTheLookup()
    {
        string? seen = null;
        using var app = await StartNotFoundAsync(options => options.UseMessages((code, culture) =>
        {
            seen = culture;
            return null;
        }));

        await app.GetProblemAsync("/x?culture=../../evil");

        seen.ShouldBe("en");
    }

    [Fact]
    public async Task FailingLookup_StillGivesAResponseAndIsLogged()
    {
        using var app = await StartNotFoundAsync(options => options.UseMessages((code, culture) => throw new NotSupportedException("store failure")));

        var problem = await app.GetProblemAsync("/x?culture=ru");

        problem.Status.ShouldBe(404);
        problem.Get("title").ShouldBe("Не найдено");
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Exception is NotSupportedException);
    }

    private static void UseResources(FaultTraceAspNetCoreOptions options)
    {
        options.UseMessages((code, culture) =>
        {
            var info = new CultureInfo(culture);
            var title = Resources.GetString($"FaultTrace.{code}.Title", info);
            var detail = Resources.GetString($"FaultTrace.{code}.Detail", info);

            return title == null || detail == null ? null : new FaultMessage(title, detail);
        });
    }

    private static Task<TestApp> StartNotFoundAsync(Action<FaultTraceAspNetCoreOptions> configure)
    {
        return TestApp.StartAsync(
            context =>
            {
                context.Response.StatusCode = 404;
                return Task.CompletedTask;
            },
            configure);
    }
}
