using System;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FaultTrace.AspNetCore.Tests.Handling;

// A browser hides a cross-origin response without these headers, so the frontend would see no error details.
public class CorsTests
{
    [Fact]
    public async Task ExceptionResponse_KeepsCorsHeaders()
    {
        using var app = await StartAsync(_ => throw new InvalidOperationException());

        var problem = await app.GetProblemAsync(prepare: request => request.Headers.Add("Origin", "https://app.example"));

        problem.Status.ShouldBe(500);
        problem.Headers["Access-Control-Allow-Origin"].ShouldBe("*");
    }

    [Fact]
    public async Task EmptyStatusResponse_KeepsCorsHeaders()
    {
        using var app = await StartAsync(context =>
        {
            context.Response.StatusCode = 404;
            return Task.CompletedTask;
        });

        var problem = await app.GetProblemAsync(prepare: request => request.Headers.Add("Origin", "https://app.example"));

        problem.Status.ShouldBe(404);
        problem.Headers["Access-Control-Allow-Origin"].ShouldBe("*");
    }

    private static Task<TestApp> StartAsync(RequestDelegate handler)
    {
        return TestApp.StartAsync(
            handler,
            services: services => services.AddCors(),
            afterFaultTrace: pipeline => pipeline.UseCors(policy => policy.AllowAnyOrigin()));
    }
}
