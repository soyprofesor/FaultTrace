using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FaultTrace.AspNetCore.Tests.Handling;

public class LogContextTests
{
    [Fact]
    public async Task Log_HasEndpointMethodAndAnonymousUser()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException());

        var problem = await app.GetProblemAsync("/api/order/add?secret=1");

        var fault = app.Logs.Faults.Single();
        fault.Properties["Endpoint"].ShouldBe("/api/order/add");
        fault.Properties["Method"].ShouldBe("GET");
        fault.Properties["User"].ShouldBe("anonymous");
        fault.Properties["ErrorId"].ShouldBe(problem.Get("errorId"));
        fault.Properties["TraceId"].ShouldBe(problem.Get("traceId"));
        fault.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task Log_EndpointKeepsUrlEncoding_SoALineBreakCantForgeALogLine()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException());

        await app.GetProblemAsync("/a%0d%0aForged");

        app.Logs.Faults.Single().Properties["Endpoint"].ShouldBe("/a%0D%0AForged");
    }

    [Fact]
    public async Task Log_HasCulture()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException());

        await app.GetProblemAsync("/x?culture=ru-RU");

        app.Logs.Faults.Single().Properties["Culture"].ShouldBe("ru");
    }

    [Fact]
    public async Task User_ComesFromTheSubClaim()
    {
        using var app = await StartWithClaimsAsync(new Claim("sub", "user-1"), new Claim(ClaimTypes.NameIdentifier, "user-2"));

        await app.GetProblemAsync();

        app.Logs.Faults.Single().Properties["User"].ShouldBe("user-1");
    }

    [Fact]
    public async Task User_FallsBackToTheMappedNameIdentifier()
    {
        using var app = await StartWithClaimsAsync(new Claim(ClaimTypes.NameIdentifier, "user-2"));

        await app.GetProblemAsync();

        app.Logs.Faults.Single().Properties["User"].ShouldBe("user-2");
    }

    [Fact]
    public async Task User_ComesFromTheConfiguredClaim()
    {
        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            options => options.UserIdClaim = "employee_id",
            afterFaultTrace: SetClaims(new Claim("sub", "user-1"), new Claim("employee_id", "user-3")));

        await app.GetProblemAsync();

        app.Logs.Faults.Single().Properties["User"].ShouldBe("user-3");
    }

    [Fact]
    public async Task User_ComesFromTheDelegate()
    {
        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            options => options.UserIdResolver = context => context.Request.Headers["X-User"],
            afterFaultTrace: SetClaims(new Claim("sub", "user-1")));

        await app.GetProblemAsync(prepare: request => request.Headers.Add("X-User", "user-4"));

        app.Logs.Faults.Single().Properties["User"].ShouldBe("user-4");
    }

    [Fact]
    public async Task User_DelegateFailure_IsAnonymousAndLogged()
    {
        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            options => options.UserIdResolver = _ => throw new NotSupportedException("resolver failure"));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        app.Logs.Faults.Single().Properties["User"].ShouldBe("anonymous");
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Exception is NotSupportedException);
    }

    [Fact]
    public async Task LocalCatchThenRethrow_KeepsTheErrorId_LogsOnce_WithRequestContext()
    {
        string? localErrorId = null;
        using var app = await TestApp.StartAsync(context =>
        {
            var exception = new InvalidOperationException();
            localErrorId = context.RequestServices.GetRequiredService<IFaultTranslator>().Translate(exception).ErrorId;
            throw exception;
        });

        var problem = await app.GetProblemAsync("/api/order/add");

        problem.Get("errorId").ShouldBe(localErrorId);
        var fault = app.Logs.Faults.Single();
        fault.Properties["Endpoint"].ShouldBe("/api/order/add");
    }

    private static Task<TestApp> StartWithClaimsAsync(params Claim[] claims)
    {
        return TestApp.StartAsync(_ => throw new InvalidOperationException(), afterFaultTrace: SetClaims(claims));
    }

    private static Action<IApplicationBuilder> SetClaims(params Claim[] claims)
    {
        return pipeline => pipeline.Use((context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            return next();
        });
    }
}
