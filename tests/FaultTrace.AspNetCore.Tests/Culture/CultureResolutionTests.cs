using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Configuration;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FaultTrace.AspNetCore.Tests.Culture;

public class CultureResolutionTests
{
    private const string English = "Unexpected error";
    private const string Russian = "Непредвиденная ошибка";
    private const string UzbekLatin = "Kutilmagan xatolik";
    private const string UzbekCyrillic = "Кутилмаган хатолик";

    [Fact]
    public async Task NoCulture_IsEnglish()
    {
        using var app = await StartAsync();

        (await TitleAsync(app)).ShouldBe(English);
    }

    [Fact]
    public async Task NoCulture_UsesTheProjectDefault()
    {
        using var app = await StartAsync(options => options.DefaultCulture = "ru");

        (await TitleAsync(app)).ShouldBe(Russian);
    }

    [Fact]
    public async Task DetailAndTitle_UseTheSameCulture()
    {
        using var app = await StartAsync();

        var problem = await app.GetProblemAsync("/x?culture=ru");

        problem.Get("title").ShouldBe(Russian);
        problem.Get("detail").ShouldStartWith("Произошла непредвиденная ошибка");
    }

    [Theory]
    [InlineData("/x?culture=ru")]
    [InlineData("/x?lang=ru")]
    [InlineData("/x?CULTURE=ru-RU")]
    public async Task Query_IsRead(string url)
    {
        using var app = await StartAsync();

        (await TitleAsync(app, url)).ShouldBe(Russian);
    }

    [Fact]
    public async Task Header_IsRead()
    {
        using var app = await StartAsync();

        (await TitleAsync(app, headers: ("X-Culture", "uz-Cyrl-UZ"))).ShouldBe(UzbekCyrillic);
    }

    [Fact]
    public async Task RouteValue_IsRead()
    {
        using var app = await TestApp.StartAsync(
            _ => throw new NotSupportedException("unreachable"),
            services: services => services.AddRouting(),
            afterFaultTrace: pipeline =>
            {
                pipeline.UseRouting();
                pipeline.UseEndpoints(endpoints => endpoints.Map("{culture}/orders", _ => throw new InvalidOperationException()));
            });

        (await TitleAsync(app, "/ru/orders")).ShouldBe(Russian);
    }

    [Fact]
    public async Task Cookie_IsRead()
    {
        using var app = await StartAsync();

        (await TitleAsync(app, headers: ("Cookie", "culture=ru"))).ShouldBe(Russian);
    }

    [Fact]
    public async Task Claim_IsRead()
    {
        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            afterFaultTrace: pipeline => pipeline.Use((context, next) =>
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("locale", "ru") }));
                return next();
            }));

        (await TitleAsync(app)).ShouldBe(Russian);
    }

    [Fact]
    public async Task AcceptLanguage_RespectsWeights()
    {
        using var app = await StartAsync();

        (await TitleAsync(app, headers: ("Accept-Language", "en;q=0.2, ru;q=0.9, uz;q=0.5"))).ShouldBe(Russian);
    }

    [Fact]
    public async Task AcceptLanguage_SkipsUnsupportedAndRefusedValues()
    {
        using var app = await StartAsync();

        (await TitleAsync(app, headers: ("Accept-Language", "de, ru;q=0, *;q=0.9, uz;q=0.1"))).ShouldBe(UzbekLatin);
    }

    [Fact]
    public async Task AcceptLanguage_Malformed_FallsBackToDefault()
    {
        using var app = await StartAsync();

        (await TitleAsync(app, headers: ("Accept-Language", ";;;q=abc,,,===="))).ShouldBe(English);
    }

    [Fact]
    public async Task Resolver_IsTheLastSource()
    {
        using var app = await StartAsync(options => options.Culture.Resolver = _ => "ru");

        (await TitleAsync(app)).ShouldBe(Russian);
        (await TitleAsync(app, headers: ("Accept-Language", "uz"))).ShouldBe(UzbekLatin);
    }

    [Fact]
    public async Task Resolver_Failure_IsLoggedAndFallsBack()
    {
        using var app = await StartAsync(options => options.Culture.Resolver = _ => throw new NotSupportedException("resolver failure"));

        (await TitleAsync(app)).ShouldBe(English);
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Exception is NotSupportedException);
    }

    [Fact]
    public async Task Order_QueryBeatsHeaderBeatsCookieBeatsAcceptLanguage()
    {
        using var app = await StartAsync();

        (await TitleAsync(app, "/x?culture=ru", ("X-Culture", "uz"), ("Cookie", "culture=uz-Cyrl"), ("Accept-Language", "en"))).ShouldBe(Russian);
        (await TitleAsync(app, "/x", ("X-Culture", "uz"), ("Cookie", "culture=uz-Cyrl"), ("Accept-Language", "en"))).ShouldBe(UzbekLatin);
        (await TitleAsync(app, "/x", ("Cookie", "culture=uz-Cyrl"), ("Accept-Language", "ru"))).ShouldBe(UzbekCyrillic);
    }

    [Theory]
    [InlineData("oz")]
    [InlineData("1")]
    [InlineData("../../etc/passwd")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task UnsupportedValue_MovesToTheNextSource(string value)
    {
        using var app = await StartAsync();

        (await TitleAsync(app, "/x?culture=" + Uri.EscapeDataString(value), ("X-Culture", "ru"))).ShouldBe(Russian);
    }

    [Fact]
    public async Task Alias_IsResolved()
    {
        using var app = await StartAsync(options => options.AddCultureAlias("oz", "uz-Latn"));

        (await TitleAsync(app, "/x?culture=oz")).ShouldBe(UzbekLatin);
    }

    [Fact]
    public async Task ProjectCulture_IsResolved()
    {
        using var app = await StartAsync(options => options.SetMessage("kk", "unexpected", "Kk title", "Kk detail"));

        (await TitleAsync(app, "/x?culture=kk")).ShouldBe("Kk title");
    }

    [Fact]
    public async Task ConfiguredNames_ReplaceTheDefaults()
    {
        using var app = await StartAsync(options =>
        {
            options.Culture.QueryNames = new List<string> { "language" };
            options.Culture.HeaderNames = new List<string>();
        });

        (await TitleAsync(app, "/x?language=ru")).ShouldBe(Russian);
        (await TitleAsync(app, "/x?culture=ru")).ShouldBe(English);
        (await TitleAsync(app, "/x", ("X-Culture", "ru"))).ShouldBe(English);
    }

    [Fact]
    public async Task AcceptLanguage_CanBeTurnedOff()
    {
        using var app = await StartAsync(options => options.Culture.UseAcceptLanguage = false);

        (await TitleAsync(app, headers: ("Accept-Language", "ru"))).ShouldBe(English);
    }

    [Fact]
    public async Task RequestLocalization_IsDetectedAndWinsOverTheSources()
    {
        using var app = await StartLocalizedAsync();

        // The FaultTrace chain would pick uz, but request localization has already settled on its default, ru.
        (await TitleAsync(app, "/x", ("X-Culture", "uz"))).ShouldBe(Russian);
    }

    [Fact]
    public async Task SourcesMode_IgnoresRequestLocalization()
    {
        using var app = await StartLocalizedAsync(options => options.Culture.Mode = FaultTraceCultureMode.Sources);

        (await TitleAsync(app, "/x", ("X-Culture", "uz"))).ShouldBe(UzbekLatin);
    }

    [Fact]
    public async Task RequestLocalizationMode_WithoutTheFeature_IsDefaultAndIgnoresTheSources()
    {
        using var app = await StartAsync(options => options.Culture.Mode = FaultTraceCultureMode.RequestLocalization);

        (await TitleAsync(app, "/x?culture=ru")).ShouldBe(English);
    }

    [Fact]
    public async Task ThreadCulture_IsNotChanged()
    {
        string? seen = null;
        var before = CultureInfo.CurrentUICulture.Name;
        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            beforeFaultTrace: pipeline => pipeline.Use(async (context, next) =>
            {
                await next();
                seen = CultureInfo.CurrentUICulture.Name;
            }));

        (await TitleAsync(app, "/x?culture=ru")).ShouldBe(Russian);

        seen.ShouldBe(before);
    }

    private static Task<TestApp> StartAsync(Action<FaultTraceAspNetCoreOptions>? configure = null)
    {
        return TestApp.ThrowingAsync(new InvalidOperationException(), configure);
    }

    private static Task<TestApp> StartLocalizedAsync(Action<FaultTraceAspNetCoreOptions>? configure = null)
    {
        return TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            configure,
            afterFaultTrace: pipeline => pipeline.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture("ru"),
                SupportedCultures = new List<CultureInfo> { new CultureInfo("ru") },
                SupportedUICultures = new List<CultureInfo> { new CultureInfo("ru") },
            }));
    }

    private static async Task<string?> TitleAsync(TestApp app, string url = "/x", params (string Name, string Value)[] headers)
    {
        var problem = await app.GetProblemAsync(url, request =>
        {
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        });

        return problem.Get("title");
    }
}
