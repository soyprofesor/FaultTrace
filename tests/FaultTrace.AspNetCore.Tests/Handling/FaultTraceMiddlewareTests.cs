using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using Driver = global::Oracle.ManagedDataAccess.Client;

namespace FaultTrace.AspNetCore.Tests.Handling;

public class FaultTraceMiddlewareTests
{
    private const string RequiredFieldMessage =
        "ORA-01400: cannot insert NULL into (\"APP\".\"ORDERS\".\"TREASURY_CODE\")\n" +
        "ORA-06512: at \"APP.ORDER_REQ\", line 1749\n" +
        "ORA-06512: at \"APP.ORDER_PACK\", line 775\n" +
        "ORA-06512: at line 1";

    private const string BusinessMessage =
        "ORA-20001: ORA-20001: Order is already closed\n" +
        "ORA-06512: at \"APP.ORDER_PACK\", line 12\n" +
        "ORA-06512: at line 1";

    [Fact]
    public async Task SuccessfulResponse_IsNotTouched()
    {
        using var app = await TestApp.StartAsync(context => context.Response.WriteAsync("done"));

        using var response = await app.Client.GetAsync("/ok");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("done");
        app.Logs.Faults.ShouldBeEmpty();
    }

    [Fact]
    public async Task NonDatabaseException_IsGenericUnexpected()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException("secret detail"));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        problem.Get("code").ShouldBe(FaultCodes.Unexpected);
        problem.Text.ShouldNotContain("secret detail");
        problem.Text.ShouldNotContain("InvalidOperationException");
        app.Logs.Faults.Single().Level.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public async Task OracleTechnicalError_LeaksNothingOutsideDebug()
    {
        using var app = await TestApp.ThrowingAsync(new Driver.OracleException(RequiredFieldMessage, 1400));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(400);
        problem.Get("code").ShouldBe(FaultCodes.RequiredField);
        foreach (var secret in new[] { "ORA-", "APP.", "ORDERS", "TREASURY_CODE", "1749", "at line" })
        {
            problem.Text.ShouldNotContain(secret);
        }
    }

    [Fact]
    public async Task OracleBusinessError_ShowsOnlyTheDeveloperText()
    {
        using var app = await TestApp.ThrowingAsync(new Driver.OracleException(BusinessMessage, 20001));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(400);
        problem.Get("code").ShouldBe(FaultCodes.Business);
        problem.Get("detail").ShouldBe("Order is already closed");
    }

    [Fact]
    public async Task RequestCancelled_IsClientClosedAtInformationLevel()
    {
        using var app = await StartAbortedAsync(new OperationCanceledException());

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(499);
        problem.Get("code").ShouldBe(FaultCodes.ClientClosed);
        app.Logs.Faults.Single().Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task IoFailureAfterTheClientLeft_IsClientClosed()
    {
        using var app = await StartAbortedAsync(new IOException("connection reset"));

        (await app.GetProblemAsync()).Status.ShouldBe(499);
    }

    [Fact]
    public async Task IoFailureWithTheClientStillThere_IsUnexpected()
    {
        using var app = await TestApp.ThrowingAsync(new IOException("disk failure"));

        (await app.GetProblemAsync()).Status.ShouldBe(500);
    }

    [Fact]
    public async Task BugWhileTheClientLeaves_StaysUnexpected()
    {
        using var app = await StartAbortedAsync(new NullReferenceException());

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        app.Logs.Faults.Single().Level.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public async Task CancellationNotFromTheClient_IsTimeout()
    {
        using var app = await TestApp.ThrowingAsync(new TaskCanceledException());

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(504);
        problem.Get("code").ShouldBe(FaultCodes.Timeout);
    }

    [Fact]
    public async Task TimeoutException_IsTimeout()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException("wrapper", new TimeoutException()));

        (await app.GetProblemAsync()).Status.ShouldBe(504);
    }

    [Fact]
    public async Task Ora01013_WithRequestAborted_IsClientClosed()
    {
        using var app = await StartAbortedAsync(new Driver.OracleException("ORA-01013: user requested cancel of current operation", 1013));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(499);
        problem.Get("code").ShouldBe(FaultCodes.ClientClosed);
        app.Logs.Faults.Single().Properties["NativeCode"].ShouldBe("ORA-01013");
    }

    [Fact]
    public async Task Ora01013_WithoutRequestAborted_IsTimeout()
    {
        using var app = await TestApp.ThrowingAsync(new Driver.OracleException("ORA-01013: user requested cancel of current operation", 1013));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(504);
        problem.Get("code").ShouldBe(FaultCodes.Timeout);
    }

    [Fact]
    public async Task ResponseAlreadyStarted_LogsAndAborts()
    {
        using var app = await TestApp.StartAsync(async context =>
        {
            await context.Response.WriteAsync("partial");
            await context.Response.Body.FlushAsync();
            throw new InvalidOperationException("late");
        });

        using var response = await app.Client.GetAsync("/stream", HttpCompletionOption.ResponseHeadersRead);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await ReadUntilFailureAsync(response);

        body.ShouldNotContain("errorId");
        var fault = app.Logs.Faults.Single();
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Text.Contains("after the response started") && entry.Text.Contains((string)fault.Properties["ErrorId"]!));
    }

    [Fact]
    public async Task ClientLeavesDuringStreaming_IsNotLoggedAsAnError()
    {
        using var app = await TestApp.StartAsync(
            async context =>
            {
                await context.Response.WriteAsync("partial");
                await context.Response.Body.FlushAsync();
                throw new OperationCanceledException();
            },
            beforeFaultTrace: pipeline => pipeline.Use((context, next) =>
            {
                context.RequestAborted = new CancellationToken(true);
                return next();
            }));

        using var response = await app.Client.GetAsync("/stream", HttpCompletionOption.ResponseHeadersRead);
        await ReadUntilFailureAsync(response);

        app.Logs.Faults.Single().Properties["Code"].ShouldBe(FaultCodes.ClientClosed);
        app.Logs.FaultTraceEntries.ShouldAllBe(entry => entry.Level == LogLevel.Information);
    }

    [Fact]
    public async Task TranslatorFailure_WritesFallbackAndLogsTheOriginalError()
    {
        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException("original"),
            services: services => services.AddSingleton<IFaultTranslator, ThrowingTranslator>());

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        problem.ContentType.ShouldBe("application/problem+json");
        problem.Get("code").ShouldBe(FaultCodes.Unexpected);
        problem.Get("detail").ShouldBe("An unexpected error occurred. Please contact support and provide the error ID.");
        var errorId = problem.Get("errorId");
        errorId.ShouldNotBeNull();
        errorId!.Length.ShouldBe(9);
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Exception is InvalidOperationException && entry.Text.Contains(errorId));
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Exception is NotSupportedException && entry.Text.Contains(errorId));
    }

    [Fact]
    public async Task WriterFailure_FallbackKeepsTheLoggedErrorId()
    {
        // The translator survives a broken message provider by itself, so the failure happens later, in the writer.
        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException("original"),
            services: services => services.AddSingleton<IFaultMessageProvider, ThrowingMessages>());

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        problem.Get("title").ShouldBe("Unexpected error");
        problem.Get("errorId").ShouldBe((string)app.Logs.Faults.Single().Properties["ErrorId"]!);
    }

    [Fact]
    public async Task Hooks_RunOnce()
    {
        var hook = new CountingHook();
        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            services: services => services.AddSingleton<IFaultHook>(hook));

        await app.GetProblemAsync();

        hook.Count.ShouldBe(1);
    }

    private static Task<TestApp> StartAbortedAsync(Exception exception)
    {
        return TestApp.StartAsync(
            _ => throw exception,
            beforeFaultTrace: app => app.Use((context, next) =>
            {
                context.RequestAborted = new CancellationToken(true);
                return next();
            }));
    }

    private static async Task<string> ReadUntilFailureAsync(HttpResponseMessage response)
    {
        var buffer = new byte[256];
        var total = new MemoryStream();
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory())) > 0)
            {
                total.Write(buffer, 0, read);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is OperationCanceledException)
        {
            // Expected: the server aborted the response.
        }

        return System.Text.Encoding.UTF8.GetString(total.ToArray());
    }

    private sealed class ThrowingTranslator : IFaultTranslator
    {
        public Fault Translate(Exception exception, string? culture = null) => throw new NotSupportedException("translator failure");
    }

    // The first lookup comes from the translator and works. The next one comes from the writer and fails.
    private sealed class ThrowingMessages : IFaultMessageProvider
    {
        private int _calls;

        public string ResolveCulture(string? culture) => "en";

        public string? FindCulture(string? culture) => null;

        public FaultMessage? Find(string code, string culture) => null;

        public FaultMessage Get(string code, string culture) => Interlocked.Increment(ref _calls) == 1
            ? new FaultMessage("x", "y")
            : throw new NotSupportedException("messages failure");
    }

    private sealed class CountingHook : IFaultHook
    {
        private int _count;

        public int Count => _count;

        public void OnFault(Fault fault, Exception? exception) => Interlocked.Increment(ref _count);
    }
}
