using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Configuration;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace FaultTrace.AspNetCore.Tests.Handling;

public class HandlingOptionsTests
{
    [Fact]
    public async Task HandleEmptyStatusOff_LeavesTheResponseEmpty()
    {
        using var app = await StartWithStatusAsync(404, options => options.HandleEmptyStatus = false);

        using var response = await app.Client.GetAsync("/x");

        ((int)response.StatusCode).ShouldBe(404);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        app.Logs.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleEmptyStatusOff_StillHandlesExceptions()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException("secret"), options => options.HandleEmptyStatus = false);

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        problem.Text.ShouldNotContain("secret");
    }

    [Fact]
    public async Task Filter_ExcludesSomeRequests()
    {
        using var app = await StartWithStatusAsync(404, options =>
            options.EmptyStatusFilter = context => !context.Request.Path.StartsWithSegments("/legacy"));

        using var legacy = await app.Client.GetAsync("/legacy/x");
        (await legacy.Content.ReadAsStringAsync()).ShouldBeEmpty();

        (await app.GetProblemAsync("/api/x")).Get("code").ShouldBe(FaultCodes.NotFound);
    }

    [Fact]
    public async Task FailingFilter_IsLoggedAndKeepsTheTemplate()
    {
        using var app = await StartWithStatusAsync(404, options =>
            options.EmptyStatusFilter = _ => throw new NotSupportedException("filter failure"));

        (await app.GetProblemAsync()).Get("code").ShouldBe(FaultCodes.NotFound);
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Exception is NotSupportedException);
    }

    [Fact]
    public async Task HandleEmptyStatus_ReloadsWithoutRestart()
    {
        var monitor = new Monitor(new FaultTraceAspNetCoreOptions());
        using var app = await TestApp.StartAsync(
            context =>
            {
                context.Response.StatusCode = 404;
                return Task.CompletedTask;
            },
            services: services => services.AddSingleton<IOptionsMonitor<FaultTraceAspNetCoreOptions>>(monitor));

        (await app.GetProblemAsync()).Get("code").ShouldBe(FaultCodes.NotFound);

        monitor.CurrentValue = new FaultTraceAspNetCoreOptions { HandleEmptyStatus = false };

        using var response = await app.Client.GetAsync("/x");
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleValidationOff_KeepsTheMvcFormat()
    {
        using var app = await StartControllersAsync(options => options.HandleValidation = false);

        using var response = await app.Client.PostAsync("/api/orders", Json("{}"));
        var problem = await Problem.ReadAsync(response);

        problem.Status.ShouldBe(400);
        problem.Has("errorId").ShouldBeFalse();
        problem.Json.GetProperty("errors").GetProperty("Name")[0].GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleValidationOff_KeepsTheConsumerFactory()
    {
        using var app = await StartControllersAsync(
            options => options.HandleValidation = false,
            services => services.Configure<ApiBehaviorOptions>(options =>
                options.InvalidModelStateResponseFactory = _ => new ContentResult { Content = "custom", StatusCode = 422 }));

        using var response = await app.Client.PostAsync("/api/orders", Json("{}"));

        ((int)response.StatusCode).ShouldBe(422);
        (await response.Content.ReadAsStringAsync()).ShouldBe("custom");
    }

    [Fact]
    public async Task HandleValidationOff_LeavesApiControllerNotFoundToMvc()
    {
        using var app = await StartControllersAsync(options => options.HandleValidation = false);

        using var response = await app.Client.GetAsync("/api/orders/missing");
        var problem = await Problem.ReadAsync(response);

        problem.Status.ShouldBe(404);
        problem.Has("errorId").ShouldBeFalse();
    }

    [Fact]
    public async Task EmptyServerError_IsLoggedAsAnErrorWithoutAnException()
    {
        using var app = await StartWithStatusAsync(500);

        await app.GetProblemAsync();

        var log = app.Logs.Faults.Single();
        log.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Error);
        log.Exception.ShouldBeNull();
    }

    [Theory]
    [InlineData(429, true)]
    [InlineData(503, true)]
    [InlineData(504, true)]
    [InlineData(500, false)]
    [InlineData(404, false)]
    public async Task EmptyStatus_SaysWhetherRetryingMakesSense(int status, bool transient)
    {
        var hook = new RecordingHook();
        using var app = await TestApp.StartAsync(
            context =>
            {
                context.Response.StatusCode = status;
                return Task.CompletedTask;
            },
            services: services => services.AddSingleton<IFaultHook>(hook));

        await app.GetProblemAsync();

        hook.Last!.IsTransient.ShouldBe(transient);
    }

    [Fact]
    public async Task OwnRetryAfter_IsKept()
    {
        using var app = await TestApp.StartAsync(context =>
        {
            context.Response.StatusCode = 503;
            context.Response.Headers["Retry-After"] = "120";
            return Task.CompletedTask;
        });

        (await app.GetProblemAsync()).Headers["Retry-After"].ShouldBe("120");
    }

    [Fact]
    public async Task EmptyUnavailable_SendsTheDefaultRetryAfter()
    {
        using var app = await StartWithStatusAsync(503);

        (await app.GetProblemAsync()).Headers["Retry-After"].ShouldBe("5");
    }

    [Fact]
    public async Task TooManyRequests_KeepsItsRetryAfterAndAddsNoDefault()
    {
        using var app = await StartWithStatusAsync(429);

        (await app.GetProblemAsync()).Headers.ContainsKey("Retry-After").ShouldBeFalse();
    }

    private static Task<TestApp> StartWithStatusAsync(int status, Action<FaultTraceAspNetCoreOptions>? configure = null)
    {
        return TestApp.StartAsync(
            context =>
            {
                context.Response.StatusCode = status;
                return Task.CompletedTask;
            },
            configure);
    }

    private static Task<TestApp> StartControllersAsync(Action<FaultTraceAspNetCoreOptions> configure, Action<IServiceCollection>? services = null)
    {
        return TestApp.StartAsync(
            context => Task.CompletedTask,
            configure,
            collection =>
            {
                collection.AddControllers().AddApplicationPart(typeof(HandlingOptionsTests).Assembly);
                services?.Invoke(collection);
            },
            afterFaultTrace: pipeline =>
            {
                pipeline.UseRouting();
                pipeline.UseEndpoints(endpoints => endpoints.MapControllers());
            });
    }

    private static StringContent Json(string json) => new StringContent(json, Encoding.UTF8, "application/json");

    private sealed class RecordingHook : IFaultHook
    {
        public Fault? Last { get; private set; }

        public void OnFault(Fault fault, Exception? exception) => Last = fault;
    }

    private sealed class Monitor : IOptionsMonitor<FaultTraceAspNetCoreOptions>
    {
        public Monitor(FaultTraceAspNetCoreOptions value)
        {
            CurrentValue = value;
        }

        public FaultTraceAspNetCoreOptions CurrentValue { get; set; }

        public FaultTraceAspNetCoreOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<FaultTraceAspNetCoreOptions, string?> listener) => null;
    }
}
