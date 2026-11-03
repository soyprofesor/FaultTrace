using System;
using System.Linq;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Results;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Driver = global::Oracle.ManagedDataAccess.Client;

namespace FaultTrace.AspNetCore.Tests.Results;

public class FaultResultTests
{
    internal const string DuplicateMessage =
        "ORA-00001: unique constraint (APP.ORDERS_PK) violated\n" +
        "ORA-06512: at \"APP.ORDER_PACK\", line 12\n" +
        "ORA-06512: at line 1";

    [Fact]
    public async Task ControllerHelper_WritesTheTemplateAndDoesNotLogAgain()
    {
        using var app = await StartControllersAsync();

        using var response = await app.Client.GetAsync("/api/caught/oracle");
        var problem = await Problem.ReadAsync(response);

        problem.Status.ShouldBe(409);
        problem.ContentType.ShouldBe("application/problem+json");
        problem.Get("code").ShouldBe(FaultCodes.Duplicate);
        problem.Get("title").ShouldBe("Already exists");
        problem.Get("instance").ShouldBe("/api/caught/oracle");
        problem.Text.ShouldNotContain("ORA-");
        problem.Text.ShouldNotContain("ORDERS_PK");

        app.Logs.Faults.Single().Properties["ErrorId"].ShouldBe(problem.Get("errorId"));
    }

    [Fact]
    public async Task ControllerHelper_UsesTheRequestCulture()
    {
        using var app = await StartControllersAsync();

        using var response = await app.Client.GetAsync("/api/caught/oracle?culture=ru");
        var problem = await Problem.ReadAsync(response);

        problem.Get("title").ShouldBe("Запись уже существует");
        problem.Get("detail").ShouldBe("Такая запись уже существует.");
    }

    [Fact]
    public async Task HandlerHelper_WritesTheTemplate()
    {
        using var app = await TestApp.StartAsync(context =>
        {
            var translator = context.RequestServices.GetRequiredService<IFaultTranslator>();
            return context.WriteFaultAsync(translator.Translate(new InvalidOperationException("secret")));
        });

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        problem.Get("code").ShouldBe(FaultCodes.Unexpected);
        problem.Text.ShouldNotContain("secret");
        app.Logs.Faults.Single().Properties["ErrorId"].ShouldBe(problem.Get("errorId"));
    }

    [Fact]
    public async Task FaultWithoutAnException_ShowsItsOwnDetailAndCategoryTitle()
    {
        using var app = await TestApp.StartAsync(context =>
        {
            var fault = context.RequestServices.GetRequiredService<IFaultFactory>()
                .Create(FaultCategory.Business, "order_closed", 400, detail: "Order is already closed");
            return context.WriteFaultAsync(fault);
        });

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(400);
        problem.Get("code").ShouldBe("order_closed");
        problem.Get("detail").ShouldBe("Order is already closed");
        problem.Get("title").ShouldBe("Request could not be completed");
        app.Logs.Faults.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Unavailable_SendsRetryAfter()
    {
        using var app = await TestApp.StartAsync(context =>
            context.WriteFaultAsync(context.RequestServices.GetRequiredService<IFaultFactory>()
                .Create(FaultCategory.Transient, FaultCodes.Unavailable, 503)));

        (await app.GetProblemAsync()).Headers["Retry-After"].ShouldBe("5");
    }

    [Fact]
    public async Task ProviderFault_ShowsDebugWhenAllowed()
    {
        using var app = await TestApp.StartAsync(TranslateOracle, environment: "Development");

        var debug = (await app.GetProblemAsync()).Json.GetProperty("debug");

        debug.GetProperty("nativeCode").GetString().ShouldBe("ORA-00001");
        debug.GetProperty("message").GetString()!.ShouldContain("ORDERS_PK");
        debug.TryGetProperty("dotnet", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ProviderFault_HidesDebugInProduction()
    {
        using var app = await TestApp.StartAsync(TranslateOracle);

        var problem = await app.GetProblemAsync();

        problem.Has("debug").ShouldBeFalse();
        problem.Text.ShouldNotContain("ORDERS_PK");
    }

    [Fact]
    public async Task FaultWithoutProvider_NeverHasDebug()
    {
        using var app = await TestApp.StartAsync(
            context => context.WriteFaultAsync(context.RequestServices.GetRequiredService<IFaultFactory>()
                .Create(FaultCategory.NotFound, FaultCodes.NotFound, 404)),
            environment: "Development");

        (await app.GetProblemAsync()).Has("debug").ShouldBeFalse();
    }

    [Fact]
    public void NullFault_IsRejected()
    {
        Should.Throw<ArgumentNullException>(() => new DefaultHttpContext().WriteFaultAsync(null!));
        Should.Throw<ArgumentNullException>(() => new CaughtController().FaultResult(null!));
    }

    private static Task TranslateOracle(HttpContext context)
    {
        var translator = context.RequestServices.GetRequiredService<IFaultTranslator>();
        return context.WriteFaultAsync(translator.Translate(new Driver.OracleException(DuplicateMessage, 1)));
    }

    private static Task<TestApp> StartControllersAsync()
    {
        return TestApp.StartAsync(
            context => Task.CompletedTask,
            services: collection => collection.AddControllers().AddApplicationPart(typeof(FaultResultTests).Assembly),
            afterFaultTrace: pipeline =>
            {
                pipeline.UseRouting();
                pipeline.UseEndpoints(endpoints => endpoints.MapControllers());
            });
    }
}

[ApiController]
[Route("api/caught")]
public class CaughtController : ControllerBase
{
    [HttpGet("oracle")]
    public IActionResult Oracle([FromServices] IFaultTranslator translator)
    {
        try
        {
            throw new Driver.OracleException(FaultResultTests.DuplicateMessage, 1);
        }
        catch (Exception exception)
        {
            return this.FaultResult(translator.Translate(exception));
        }
    }
}
