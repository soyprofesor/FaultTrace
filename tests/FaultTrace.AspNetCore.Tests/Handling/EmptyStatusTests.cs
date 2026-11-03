using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace FaultTrace.AspNetCore.Tests.Handling;

public class EmptyStatusTests
{
    [Theory]
    [InlineData(401, FaultCodes.Unauthorized, "unauthorized", "Authentication required", LogLevel.Information)]
    [InlineData(403, FaultCodes.Forbidden, "forbidden", "Access denied", LogLevel.Information)]
    [InlineData(404, FaultCodes.NotFound, "not-found", "Not found", LogLevel.Information)]
    [InlineData(405, FaultCodes.MethodNotAllowed, "method-not-allowed", "Method not allowed", LogLevel.Information)]
    [InlineData(415, FaultCodes.UnsupportedMediaType, "unsupported-media-type", "Unsupported format", LogLevel.Warning)]
    [InlineData(400, FaultCodes.Validation, "validation", "Invalid data", LogLevel.Warning)]
    [InlineData(408, FaultCodes.Business, "business", "Request could not be completed", LogLevel.Warning)]
    [InlineData(409, FaultCodes.Conflict, "conflict", "Conflict", LogLevel.Warning)]
    [InlineData(410, FaultCodes.NotFound, "not-found", "Not found", LogLevel.Information)]
    [InlineData(413, FaultCodes.Business, "business", "Request could not be completed", LogLevel.Warning)]
    [InlineData(429, FaultCodes.TooManyRequests, "too-many-requests", "Too many requests", LogLevel.Warning)]
    [InlineData(499, FaultCodes.ClientClosed, "client-closed", "Request cancelled", LogLevel.Information)]
    [InlineData(500, FaultCodes.Unexpected, "unexpected", "Unexpected error", LogLevel.Error)]
    [InlineData(501, FaultCodes.Unexpected, "unexpected", "Unexpected error", LogLevel.Error)]
    [InlineData(502, FaultCodes.Unexpected, "unexpected", "Unexpected error", LogLevel.Error)]
    [InlineData(503, FaultCodes.Unavailable, "unavailable", "Service unavailable", LogLevel.Warning)]
    [InlineData(504, FaultCodes.Timeout, "timeout", "Request timed out", LogLevel.Warning)]
    public async Task EmptyStatus_GetsTheTemplateAndIsLogged(int status, string code, string typeName, string title, LogLevel level)
    {
        using var app = await StartWithStatusAsync(status);

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(status);
        problem.ContentType.ShouldBe("application/problem+json");
        problem.Get("code").ShouldBe(code);
        problem.Get("type").ShouldBe("https://soyprofesor.github.io/FaultTrace/errors/" + typeName);
        problem.Get("title").ShouldBe(title);
        problem.Get("instance").ShouldBe("/api/order/add");
        problem.Has("timestamp").ShouldBeTrue();

        var log = app.Logs.Faults.Single();
        log.Level.ShouldBe(level);
        log.Exception.ShouldBeNull();
        log.Properties["ErrorId"].ShouldBe(problem.Get("errorId"));
        log.Properties["Endpoint"].ShouldBe("/api/order/add");
    }

    [Fact]
    public async Task EmptyStatus_UsesTheRequestCulture()
    {
        using var app = await StartWithStatusAsync(404);

        var problem = await app.GetProblemAsync("/missing?culture=ru");

        problem.Get("title").ShouldBe("Не найдено");
        problem.Get("detail").ShouldBe("Запрошенные данные не найдены.");
    }

    [Fact]
    public async Task EmptyStatus_KeepsTheHeadersAlreadySet()
    {
        using var app = await TestApp.StartAsync(context =>
        {
            context.Response.StatusCode = 401;
            context.Response.Headers["WWW-Authenticate"] = "Bearer";
            return Task.CompletedTask;
        });

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(401);
        problem.Headers["WWW-Authenticate"].ShouldBe("Bearer");
    }

    [Fact]
    public async Task RoutingMethodMismatch_GetsTheTemplateAndKeepsAllow()
    {
        using var app = await TestApp.StartAsync(
            context => Task.CompletedTask,
            services: services => services.AddRouting(),
            afterFaultTrace: pipeline =>
            {
                pipeline.UseRouting();
                pipeline.UseEndpoints(endpoints => endpoints.MapGet("/only-get", context => Task.CompletedTask));
            });

        using var response = await app.Client.PostAsync("/only-get", new StringContent(string.Empty));
        var problem = await Problem.ReadAsync(response);

        problem.Status.ShouldBe(405);
        problem.Get("code").ShouldBe(FaultCodes.MethodNotAllowed);
        problem.Headers["Allow"].ShouldBe("GET");
    }

    [Fact]
    public async Task EmptyStatus_NeverHasADebugBlock()
    {
        using var app = await TestApp.StartAsync(
            context =>
            {
                context.Response.StatusCode = 404;
                return Task.CompletedTask;
            },
            environment: "Development");

        (await app.GetProblemAsync()).Has("debug").ShouldBeFalse();
    }

    [Fact]
    public async Task EmptyStatus_RunsHooksWithoutAnException()
    {
        var hook = new RecordingHook();
        using var app = await TestApp.StartAsync(
            context =>
            {
                context.Response.StatusCode = 404;
                return Task.CompletedTask;
            },
            services: services => services.AddSingleton<IFaultHook>(hook));

        await app.GetProblemAsync();

        hook.Count.ShouldBe(1);
        hook.LastException.ShouldBeNull();
    }

    [Fact]
    public async Task ResponseWithBody_IsNotTouched()
    {
        using var app = await TestApp.StartAsync(async context =>
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("own body");
        });

        using var response = await app.Client.GetAsync("/x");

        (await response.Content.ReadAsStringAsync()).ShouldBe("own body");
        app.Logs.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResponseWithContentType_IsNotTouched()
    {
        using var app = await TestApp.StartAsync(context =>
        {
            context.Response.StatusCode = 404;
            context.Response.ContentType = "text/plain";
            return Task.CompletedTask;
        });

        using var response = await app.Client.GetAsync("/x");

        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        app.Logs.Faults.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(200)]
    [InlineData(204)]
    [InlineData(302)]
    [InlineData(304)]
    public async Task NonErrorStatus_IsNotTouched(int status)
    {
        using var app = await StartWithStatusAsync(status);

        using var response = await app.Client.GetAsync("/x");

        ((int)response.StatusCode).ShouldBe(status);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        app.Logs.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task LibraryFailure_WritesTheFallback()
    {
        using var app = await TestApp.StartAsync(
            context =>
            {
                context.Response.StatusCode = 404;
                return Task.CompletedTask;
            },
            services: services => services.AddSingleton<IFaultFactory, ThrowingFactory>());

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        problem.Get("code").ShouldBe(FaultCodes.Unexpected);
        var errorId = problem.Get("errorId");
        errorId.ShouldNotBeNull();
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Exception is NotSupportedException && entry.Text.Contains(errorId!));
    }

    private static Task<TestApp> StartWithStatusAsync(int status)
    {
        return TestApp.StartAsync(context =>
        {
            context.Response.StatusCode = status;
            return Task.CompletedTask;
        });
    }

    private sealed class RecordingHook : IFaultHook
    {
        private int _count;

        public int Count => _count;

        public Exception? LastException { get; private set; }

        public void OnFault(Fault fault, Exception? exception)
        {
            LastException = exception;
            Interlocked.Increment(ref _count);
        }
    }

    private sealed class ThrowingFactory : IFaultFactory
    {
        public Fault Create(
            FaultCategory category,
            string code,
            int status,
            string? culture = null,
            System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>>? errors = null,
            string? detail = null) =>
            throw new NotSupportedException("factory failure");
    }
}
