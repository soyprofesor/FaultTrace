using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Configuration;
using FaultTrace.AspNetCore.DependencyInjection;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace FaultTrace.AspNetCore.Tests.Configuration;

public class ConfigurationBindingTests
{
    [Fact]
    public async Task Section_SetsTheOptions()
    {
        var section = CreateSection(new Dictionary<string, string?>
        {
            ["FaultTrace:DetailExposure"] = "Never",
            ["FaultTrace:TypeBaseUri"] = "https://example.test/errors",
            ["FaultTrace:RetryAfterSeconds"] = "30",
            ["FaultTrace:UserIdClaim"] = "uid",
            ["FaultTrace:HandleEmptyStatus"] = "false",
            ["FaultTrace:HandleValidation"] = "false",
            ["FaultTrace:Culture:Mode"] = "Sources",
            ["FaultTrace:Culture:UseAcceptLanguage"] = "false",
            ["FaultTrace:Culture:HeaderNames:0"] = "X-Lang",
        }).GetSection("FaultTrace");

        using var app = await StartAsync(section);
        var options = app.Services.GetRequiredService<IOptions<FaultTraceAspNetCoreOptions>>().Value;

        options.DetailExposure.ShouldBe(DetailExposure.Never);
        options.TypeBaseUri.ShouldBe("https://example.test/errors");
        options.RetryAfterSeconds.ShouldBe(30);
        options.UserIdClaim.ShouldBe("uid");
        options.HandleEmptyStatus.ShouldBeFalse();
        options.HandleValidation.ShouldBeFalse();
        options.Culture.Mode.ShouldBe(FaultTraceCultureMode.Sources);
        options.Culture.UseAcceptLanguage.ShouldBeFalse();
        options.Culture.HeaderNames.ShouldBe(new[] { "X-Lang" });
    }

    [Fact]
    public async Task DefaultCulture_IsReadFromTheSection()
    {
        var section = CreateSection(new Dictionary<string, string?> { ["FaultTrace:DefaultCulture"] = "ru" }).GetSection("FaultTrace");
        using var app = await StartAsync(section);

        var problem = await app.GetProblemAsync();

        problem.Get("title").ShouldBe("Не найдено");
    }

    [Fact]
    public async Task ChangedSection_AppliesWithoutARestart()
    {
        var root = CreateSection(new Dictionary<string, string?> { ["FaultTrace:HandleEmptyStatus"] = "true" });
        using var app = await StartAsync(root.GetSection("FaultTrace"));

        (await app.GetProblemAsync()).Get("code").ShouldBe(FaultCodes.NotFound);

        root.Providers.Single().Set("FaultTrace:HandleEmptyStatus", "false");
        root.Reload();

        using var response = await app.Client.GetAsync("/x");
        ((int)response.StatusCode).ShouldBe(404);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    private static IConfigurationRoot CreateSection(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Task<TestApp> StartAsync(IConfigurationSection section)
    {
        return TestApp.StartAsync(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            },
            addFaultTrace: services => services.AddFaultTrace(section));
    }
}
