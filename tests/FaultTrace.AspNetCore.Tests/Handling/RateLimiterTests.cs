#if NET7_0_OR_GREATER
using System;
using System.Linq;
using System.Net;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Configuration;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace FaultTrace.AspNetCore.Tests.Handling;

// The real framework middleware, because what a rejection looks like is decided by the framework, not by these tests.
public class RateLimiterTests
{
    [Fact]
    public async Task RequestWithinTheLimit_IsNotTouched()
    {
        using var app = await StartAsync(options => options.RejectionStatusCode = 429);

        using var response = await app.Client.GetAsync("/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("order");
        app.Logs.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejection429_GetsTheTemplate()
    {
        using var app = await StartAsync(options => options.RejectionStatusCode = 429);

        var problem = await GetRejectedAsync(app);

        problem.Status.ShouldBe(429);
        problem.ContentType.ShouldBe("application/problem+json");
        problem.Get("code").ShouldBe(FaultCodes.TooManyRequests);
        problem.Get("errorId").ShouldNotBeNullOrWhiteSpace();
        problem.Get("instance").ShouldBe("/orders");
        var entry = app.Logs.Faults.Single();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Properties["ErrorId"].ShouldBe(problem.Get("errorId"));
    }

    [Fact]
    public async Task Rejection429_IsLocalized()
    {
        using var app = await StartAsync(options => options.RejectionStatusCode = 429);
        using var first = await app.Client.GetAsync("/orders");

        var russian = await app.GetProblemAsync("/orders", request => request.Headers.Add("Accept-Language", "ru"));
        var english = await app.GetProblemAsync("/orders", request => request.Headers.Add("Accept-Language", "en"));

        russian.Status.ShouldBe(429);
        russian.Get("detail").ShouldNotBe(english.Get("detail"));
    }

    [Fact]
    public async Task Rejection429_DoesNotInventARetryAfter()
    {
        using var app = await StartAsync(options => options.RejectionStatusCode = 429);

        (await GetRejectedAsync(app)).Headers.ContainsKey("Retry-After").ShouldBeFalse();
    }

    [Fact]
    public async Task DefaultRejection_Is503WithRetryAfter()
    {
        using var app = await StartAsync(options => { });

        var problem = await GetRejectedAsync(app);

        problem.Status.ShouldBe(503);
        problem.Get("code").ShouldBe(FaultCodes.Unavailable);
        problem.Headers["Retry-After"].ShouldBe("5");
    }

    [Fact]
    public async Task RetryAfterFromTheLimiter_IsKept()
    {
        using var app = await StartAsync(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds).ToString();
                }

                return ValueTask.CompletedTask;
            };
        });

        var problem = await GetRejectedAsync(app);

        problem.Get("code").ShouldBe(FaultCodes.TooManyRequests);
        int.Parse(problem.Headers["Retry-After"]).ShouldBeGreaterThan(5);
    }

    [Fact]
    public async Task RetryAfterOn503_IsNotOverwritten()
    {
        using var app = await StartAsync(options => options.OnRejected = (context, _) =>
        {
            context.HttpContext.Response.Headers["Retry-After"] = "120";
            return ValueTask.CompletedTask;
        });

        (await GetRejectedAsync(app)).Headers["Retry-After"].ShouldBe("120");
    }

    [Fact]
    public async Task OwnBodyFromOnRejected_IsNotTouched()
    {
        using var app = await StartAsync(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (context, token) => await context.HttpContext.Response.WriteAsync("slow down", token);
        });
        using var first = await app.Client.GetAsync("/orders");

        using var response = await app.Client.GetAsync("/orders");

        ((int)response.StatusCode).ShouldBe(429);
        (await response.Content.ReadAsStringAsync()).ShouldBe("slow down");
        app.Logs.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleEmptyStatusOff_LeavesTheRejectionEmpty()
    {
        using var app = await StartAsync(options => options.RejectionStatusCode = 429, handling => handling.HandleEmptyStatus = false);
        using var first = await app.Client.GetAsync("/orders");

        using var response = await app.Client.GetAsync("/orders");

        ((int)response.StatusCode).ShouldBe(429);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        app.Logs.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task EndpointPolicy_GetsTheTemplate()
    {
        using var app = await TestApp.StartAsync(
            context => Task.CompletedTask,
            services: services =>
            {
                services.AddRouting();
                services.AddRateLimiter(options =>
                {
                    options.RejectionStatusCode = 429;
                    options.AddFixedWindowLimiter("orders", limiter =>
                    {
                        limiter.PermitLimit = 1;
                        limiter.Window = TimeSpan.FromHours(1);
                    });
                });
            },
            afterFaultTrace: pipeline =>
            {
                pipeline.UseRouting();
                pipeline.UseRateLimiter();
                pipeline.UseEndpoints(endpoints => endpoints
                    .MapGet("/orders", () => "order")
                    .RequireRateLimiting("orders"));
            });

        var problem = await GetRejectedAsync(app);

        problem.Status.ShouldBe(429);
        problem.Get("code").ShouldBe(FaultCodes.TooManyRequests);
    }

    [Fact]
    public async Task LimiterBeforeFaultTrace_IsOutOfReach()
    {
        using var app = await TestApp.StartAsync(
            context => context.Response.WriteAsync("order"),
            services: services => services.AddRateLimiter(Limit(options => options.RejectionStatusCode = 429)),
            beforeFaultTrace: pipeline => pipeline.UseRateLimiter());
        using var first = await app.Client.GetAsync("/orders");

        using var response = await app.Client.GetAsync("/orders");

        ((int)response.StatusCode).ShouldBe(429);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    private static Task<TestApp> StartAsync(Action<RateLimiterOptions> configure, Action<FaultTraceAspNetCoreOptions>? handling = null)
    {
        return TestApp.StartAsync(
            context => context.Response.WriteAsync("order"),
            handling,
            services => services.AddRateLimiter(Limit(configure)),
            afterFaultTrace: pipeline => pipeline.UseRateLimiter());
    }

    private static Action<RateLimiterOptions> Limit(Action<RateLimiterOptions> configure)
    {
        return options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ => RateLimitPartition.GetFixedWindowLimiter(
                "all",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromHours(1) }));
            configure(options);
        };
    }

    private static async Task<Problem> GetRejectedAsync(TestApp app)
    {
        using var first = await app.Client.GetAsync("/orders");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await app.GetProblemAsync("/orders");
    }
}
#endif
