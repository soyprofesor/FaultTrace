using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FaultTrace.AspNetCore.Tests.Response;

// Newtonsoft words its parser errors differently from System.Text.Json, and they must not reach the user either.
public class NewtonsoftInputTests
{
    [Theory]
    [InlineData("{\"amount\":\"abc\"}")]
    [InlineData("{\"amount\":")]
    [InlineData("not json")]
    public async Task InvalidBody_ShowsNoParserText(string body)
    {
        using var app = await StartAsync();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await app.Client.PostAsync("/api/newtonsoft", content);
        var problem = await Problem.ReadAsync(response);

        problem.Status.ShouldBe(400);
        problem.Get("code").ShouldBe(FaultCodes.Validation);
        var errors = problem.Has("errors") ? problem.Json.GetProperty("errors").GetRawText() : string.Empty;
        errors.ShouldNotContain("request");
        errors.ShouldNotContain("Could not convert");
        errors.ShouldNotContain("Unexpected character");
        errors.ShouldNotContain("Unexpected end");
        errors.ShouldNotContain("Path ");
        errors.ShouldNotContain("line 1");
        errors.ShouldNotContain("Newtonsoft");
        errors.ShouldNotContain("System.");
    }

    [Fact]
    public async Task UnreadBody_HasOnlyTheDetail()
    {
        using var app = await StartAsync();
        using var content = new StringContent("not json", Encoding.UTF8, "application/json");

        using var response = await app.Client.PostAsync("/api/newtonsoft", content);
        var problem = await Problem.ReadAsync(response);

        problem.Has("errors").ShouldBeFalse();
    }

    [Fact]
    public async Task ValidationAttribute_KeepsItsText()
    {
        using var app = await StartAsync();
        using var content = new StringContent("{\"amount\":0}", Encoding.UTF8, "application/json");

        using var response = await app.Client.PostAsync("/api/newtonsoft", content);
        var problem = await Problem.ReadAsync(response);

        problem.Json.GetProperty("errors").GetProperty("amount")[0].GetString().ShouldBe("Amount is too small");
    }

    private static Task<TestApp> StartAsync()
    {
        return TestApp.StartAsync(
            context => Task.CompletedTask,
            services: services => services
                .AddControllers()
                .AddApplicationPart(typeof(NewtonsoftOrdersController).Assembly)
                .AddNewtonsoftJson(),
            afterFaultTrace: pipeline =>
            {
                pipeline.UseRouting();
                pipeline.UseEndpoints(endpoints => endpoints.MapControllers());
            });
    }
}

public class NewtonsoftOrderRequest
{
    [System.ComponentModel.DataAnnotations.Range(1, 10, ErrorMessage = "Amount is too small")]
    public int Amount { get; set; }
}

[ApiController]
[Route("api/newtonsoft")]
public class NewtonsoftOrdersController : ControllerBase
{
    [HttpPost]
    public IActionResult Add([FromBody] NewtonsoftOrderRequest request) => Ok();
}
