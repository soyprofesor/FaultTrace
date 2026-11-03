#if NET6_0_OR_GREATER
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Results;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.AspNetCore.Tests.Response;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Driver = global::Oracle.ManagedDataAccess.Client;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace FaultTrace.AspNetCore.Tests.Results;

public class MinimalApiTests
{
    private const string DuplicateMessage = "ORA-00001: unique constraint (APP.ORDERS_PK) violated";

    [Fact]
    public async Task UncaughtException_GetsTheTemplate()
    {
        using var app = await StartAsync(endpoints => endpoints.MapPost(
            "/orders",
            (OrderRequest request) =>
            {
                throw new Driver.OracleException(DuplicateMessage, 1);
#pragma warning disable CS0162
                return HttpResults.Ok();
#pragma warning restore CS0162
            }));

        var problem = await PostAsync(app);

        problem.Status.ShouldBe(409);
        problem.Get("code").ShouldBe(FaultCodes.Duplicate);
        problem.Text.ShouldNotContain("ORA-");
    }

    [Fact]
    public async Task CaughtException_CanBeMappedToAnOwnFormat()
    {
        using var app = await StartAsync(endpoints => endpoints.MapPost(
            "/orders",
            (OrderRequest request, IFaultTranslator translator) =>
            {
                try
                {
                    throw new Driver.OracleException(DuplicateMessage, 1);
                }
                catch (Exception exception)
                {
                    var fault = translator.Translate(exception);
                    return HttpResults.Json(new { key = fault.Code, id = fault.ErrorId }, statusCode: fault.Status);
                }
            }));

        using var response = await app.Client.PostAsync("/orders", Json());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        ((int)response.StatusCode).ShouldBe(409);
        document.RootElement.GetProperty("key").GetString().ShouldBe(FaultCodes.Duplicate);
        document.RootElement.GetProperty("id").GetString().ShouldBe((string)app.Logs.Faults.Single().Properties["ErrorId"]!);
    }

    [Fact]
    public async Task CaughtException_CanBeWrittenWithTheHelper()
    {
        using var app = await StartAsync(endpoints => endpoints.MapPost(
            "/orders",
            async (OrderRequest request, HttpContext context, IFaultTranslator translator) =>
            {
                try
                {
                    throw new Driver.OracleException(DuplicateMessage, 1);
                }
                catch (Exception exception)
                {
                    await context.WriteFaultAsync(translator.Translate(exception));
                }
            }));

        var problem = await PostAsync(app);

        problem.Status.ShouldBe(409);
        problem.ContentType.ShouldBe("application/problem+json");
        problem.Get("code").ShouldBe(FaultCodes.Duplicate);
        app.Logs.Faults.Single().Properties["ErrorId"].ShouldBe(problem.Get("errorId"));
    }

#if NET7_0_OR_GREATER
    [Fact]
    public async Task HelperInAHandlerThatReturnsAResult_FinishesWithEmpty()
    {
        using var app = await StartAsync(endpoints => endpoints.MapPost(
            "/orders",
            async (HttpContext context, IFaultTranslator translator) =>
            {
                await context.WriteFaultAsync(translator.Translate(new Driver.OracleException(DuplicateMessage, 1)));
                return HttpResults.Empty;
            }));

        (await PostAsync(app)).Status.ShouldBe(409);
    }
#endif

    [Fact]
    public async Task EmptyNotFound_GetsTheTemplate()
    {
        using var app = await StartAsync(endpoints => endpoints.MapPost("/orders", () => HttpResults.NotFound()));

        var problem = await PostAsync(app);

        problem.Status.ShouldBe(404);
        problem.Get("code").ShouldBe(FaultCodes.NotFound);
    }

    [Fact]
    public async Task EmptyUnauthorized_GetsTheTemplate()
    {
        using var app = await StartAsync(endpoints => endpoints.MapPost("/orders", () => HttpResults.Unauthorized()));

        (await PostAsync(app)).Get("code").ShouldBe(FaultCodes.Unauthorized);
    }

    [Fact]
    public async Task NotFoundWithBody_IsNotTouched()
    {
        using var app = await StartAsync(endpoints => endpoints.MapPost("/orders", () => HttpResults.NotFound("no such order")));

        using var response = await app.Client.PostAsync("/orders", Json());

        (await response.Content.ReadAsStringAsync()).ShouldNotContain("errorId");
        app.Logs.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task EmptyBadRequest_GetsTheTemplate()
    {
        using var app = await StartAsync(endpoints => endpoints.MapPost("/orders", () => HttpResults.BadRequest()));

        var problem = await PostAsync(app);

        problem.Status.ShouldBe(400);
        problem.Get("code").ShouldBe(FaultCodes.Validation);
    }

    [Fact]
    public async Task FrameworkProblem_IsNotTouched()
    {
        using var app = await StartAsync(endpoints => endpoints.MapPost("/orders", () => HttpResults.Problem("own text")));

        var problem = await PostAsync(app);

        problem.Get("detail").ShouldBe("own text");
        problem.Has("errorId").ShouldBeFalse();
    }

    [Fact]
    public async Task FrameworkValidationProblem_IsNotTouched()
    {
        using var app = await StartAsync(endpoints => endpoints.MapPost(
            "/orders",
            () => HttpResults.ValidationProblem(new System.Collections.Generic.Dictionary<string, string[]> { ["name"] = ["Required"] })));

        (await PostAsync(app)).Has("errorId").ShouldBeFalse();
    }

    private static Task<TestApp> StartAsync(Action<IEndpointRouteBuilder> map)
    {
        return TestApp.StartAsync(
            context => Task.CompletedTask,
            services: services => services.AddRouting(),
            afterFaultTrace: pipeline =>
            {
                pipeline.UseRouting();
                pipeline.UseEndpoints(map);
            });
    }

    private static async Task<Problem> PostAsync(TestApp app)
    {
        using var response = await app.Client.PostAsync("/orders", Json());
        return await Problem.ReadAsync(response);
    }

    private static StringContent Json() => new("{\"name\":\"a\"}", Encoding.UTF8, "application/json");
}
#endif
