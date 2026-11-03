using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace FaultTrace.AspNetCore.Tests.Response;

public class ValidationProblemTests
{
    [Fact]
    public async Task InvalidModel_GetsTheTemplateWithFieldErrors()
    {
        using var app = await StartAsync();

        var problem = await PostAsync(app, "/api/orders", "{\"items\":[{}]}");

        problem.Status.ShouldBe(400);
        problem.ContentType.ShouldBe("application/problem+json");
        problem.Get("code").ShouldBe(FaultCodes.Validation);
        problem.Get("title").ShouldBe("Invalid data");
        problem.Get("detail").ShouldBe("Some of the submitted data is invalid.");
        problem.Has("errorId").ShouldBeTrue();

        var errors = problem.Json.GetProperty("errors");
        errors.GetProperty("name")[0].GetString().ShouldNotBeNullOrEmpty();
        errors.GetProperty("items[0].code")[0].GetString().ShouldNotBeNullOrEmpty();

        var log = app.Logs.Faults.Single();
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["ErrorId"].ShouldBe(problem.Get("errorId"));
    }

    [Fact]
    public async Task InvalidModel_UsesTheRequestCulture()
    {
        using var app = await StartAsync();

        var problem = await PostAsync(app, "/api/orders?culture=ru", "{}");

        problem.Get("title").ShouldBe("Некорректные данные");
        problem.Get("detail").ShouldBe("Часть отправленных данных некорректна.");
    }

    [Theory]
    [InlineData("{\"name\":\"a\",\"amount\":\"not a number\"}")]
    [InlineData("{\"name\":")]
    public async Task UnreadableJson_DoesNotShowTheParserMessage(string body)
    {
        using var app = await StartAsync();

        var problem = await PostAsync(app, "/api/orders", body);

        problem.Status.ShouldBe(400);
        problem.Get("code").ShouldBe(FaultCodes.Validation);
        foreach (var secret in new[] { "System.", "LineNumber", "BytePosition", "Path:", "JSON" })
        {
            problem.Text.ShouldNotContain(secret);
        }

        problem.Json.GetProperty("errors").EnumerateObject()
            .SelectMany(field => field.Value.EnumerateArray())
            .Select(message => message.GetString())
            .ShouldContain("Some of the submitted data is invalid.");
    }

    [Fact]
    public async Task UnreadableJsonField_IsNamedTheWayTheClientSentIt()
    {
        using var app = await StartAsync();

        var problem = await PostAsync(app, "/api/orders", "{\"name\":\"a\",\"amount\":\"not a number\"}");

        problem.Json.GetProperty("errors").TryGetProperty("amount", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task ErrorWithOnlyAnException_ShowsTheGenericText()
    {
        using var app = await StartAsync();

        var problem = await PostAsync(app, "/api/orders/exception", "{}");

        problem.Text.ShouldNotContain("secret");
        problem.Json.GetProperty("errors").GetProperty("code")[0].GetString().ShouldBe("Some of the submitted data is invalid.");
    }

    [Fact]
    public async Task ValidationProblemCall_GetsTheTemplate()
    {
        using var app = await StartAsync();

        var problem = await PostAsync(app, "/api/orders/manual", "{}");

        problem.Status.ShouldBe(400);
        problem.Get("code").ShouldBe(FaultCodes.Validation);
        problem.Json.GetProperty("errors").GetProperty("code")[0].GetString().ShouldBe("Bad code");
    }

    [Fact]
    public async Task ValidationProblemWithOwnDictionary_ShowsItsErrors()
    {
        using var app = await StartAsync();

        var problem = await PostAsync(app, "/api/orders/own-dictionary", "{}");

        problem.Get("code").ShouldBe(FaultCodes.Validation);
        problem.Json.GetProperty("errors").GetProperty("code")[0].GetString().ShouldBe("Own error");
    }

    [Fact]
    public async Task BadRequestWithModelState_GetsTheTemplate()
    {
        using var app = await StartAsync();

        var problem = await PostAsync(app, "/api/orders/bad-request", "{}");

        problem.Status.ShouldBe(400);
        problem.Get("code").ShouldBe(FaultCodes.Validation);
        problem.Json.GetProperty("errors").GetProperty("code")[0].GetString().ShouldBe("Bad code");
    }

    [Fact]
    public async Task ValidModel_IsNotTouched()
    {
        using var app = await StartAsync();

        using var response = await app.Client.PostAsync("/api/orders", Json("{\"name\":\"a\"}"));

        ((int)response.StatusCode).ShouldBe(200);
        app.Logs.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApiControllerEmptyNotFound_GetsTheTemplate()
    {
        using var app = await StartAsync();

        using var response = await app.Client.GetAsync("/api/orders/missing");
        var problem = await Problem.ReadAsync(response);

        problem.Status.ShouldBe(404);
        problem.Get("code").ShouldBe(FaultCodes.NotFound);
        problem.Has("errorId").ShouldBeTrue();
    }

    [Fact]
    public async Task ApiControllerWrongContentType_GetsTheTemplate()
    {
        using var app = await StartAsync();

        using var response = await app.Client.PostAsync("/api/orders", new StringContent("x", Encoding.UTF8, "text/plain"));
        var problem = await Problem.ReadAsync(response);

        problem.Status.ShouldBe(415);
        problem.Get("code").ShouldBe(FaultCodes.UnsupportedMediaType);
    }

    [Fact]
    public async Task ApiControllerNotFoundWithBody_IsNotTouched()
    {
        using var app = await StartAsync();

        using var response = await app.Client.GetAsync("/api/orders/with-body");
        var text = await response.Content.ReadAsStringAsync();

        ((int)response.StatusCode).ShouldBe(404);
        text.ShouldContain("no such order");
        text.ShouldNotContain("errorId");
    }

    [Fact]
    public async Task ConsumerFactory_IsReplacedByTheTemplate()
    {
        using var app = await StartAsync(services => services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = _ => new ContentResult { Content = "custom", StatusCode = 422 }));

        var problem = await PostAsync(app, "/api/orders", "{}");

        problem.Status.ShouldBe(400);
        problem.Get("code").ShouldBe(FaultCodes.Validation);
    }

    private static Task<TestApp> StartAsync(Action<IServiceCollection>? services = null)
    {
        return TestApp.StartAsync(
            context =>
            {
                context.Response.StatusCode = 418;
                return Task.CompletedTask;
            },
            services: collection =>
            {
                collection.AddControllers().AddApplicationPart(typeof(ValidationProblemTests).Assembly);
                services?.Invoke(collection);
            },
            afterFaultTrace: pipeline =>
            {
                pipeline.UseRouting();
                pipeline.UseEndpoints(endpoints => endpoints.MapControllers());
            });
    }

    private static async Task<Problem> PostAsync(TestApp app, string url, string json)
    {
        using var response = await app.Client.PostAsync(url, Json(json));
        return await Problem.ReadAsync(response);
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}

public class OrderRequest
{
    [Required]
    public string? Name { get; set; }

    public decimal Amount { get; set; }

    public List<OrderItemRequest>? Items { get; set; }
}

public class OrderItemRequest
{
    [Required]
    public string? Code { get; set; }
}

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    public IActionResult Add([FromBody] OrderRequest request) => Ok();

    [HttpPost("manual")]
    public IActionResult Manual()
    {
        ModelState.AddModelError("Code", "Bad code");
        return ValidationProblem();
    }

    [HttpPost("own-dictionary")]
    public IActionResult OwnDictionary()
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        modelState.AddModelError("Code", "Own error");
        return ValidationProblem(modelState);
    }

    [HttpPost("bad-request")]
    public IActionResult BadRequestWithModelState()
    {
        ModelState.AddModelError("Code", "Bad code");
        return BadRequest(ModelState);
    }

    [HttpPost("exception")]
    public IActionResult WithException()
    {
        ModelState.AddModelError("Code", new InvalidOperationException("secret"), MetadataProvider.GetMetadataForType(typeof(string)));
        return ValidationProblem();
    }

    [HttpGet("missing")]
    public IActionResult Missing() => NotFound();

    [HttpGet("with-body")]
    public IActionResult WithBody() => NotFound("no such order");
}
