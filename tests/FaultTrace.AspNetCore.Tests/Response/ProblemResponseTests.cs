using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Configuration;
using FaultTrace.AspNetCore.Response;
using FaultTrace.AspNetCore.Tests.Infrastructure;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using Driver = global::Oracle.ManagedDataAccess.Client;

namespace FaultTrace.AspNetCore.Tests.Response;

public class ProblemResponseTests
{
    private const string TechnicalMessage =
        "ORA-01400: cannot insert NULL into (\"APP\".\"ORDERS\".\"TREASURY_CODE\")\n" +
        "ORA-06512: at \"APP.ORDER_REQ\", line 1749\n" +
        "ORA-06512: at \"APP.ORDER_PACK\", line 775\n" +
        "ORA-06512: at line 1";

    [Fact]
    public async Task Body_HasAllCoreFieldsInCamelCase()
    {
        using var app = await TestApp.ThrowingAsync(new Driver.OracleException(TechnicalMessage, 1400));

        var problem = await app.GetProblemAsync("/api/order/add?x=1");

        problem.ContentType.ShouldBe("application/problem+json");
        problem.Get("type").ShouldBe("https://soyprofesor.github.io/FaultTrace/errors/required-field");
        problem.Get("title").ShouldBe("Incomplete data");
        problem.Json.GetProperty("status").GetInt32().ShouldBe(400);
        problem.Get("detail").ShouldBe("A required field is missing.");
        problem.Get("instance").ShouldBe("/api/order/add");
        problem.Get("code").ShouldBe("required_field");
        problem.Get("errorId")!.Length.ShouldBe(9);
        problem.Get("traceId").ShouldNotBeNullOrEmpty();
        DateTimeOffset.ParseExact(problem.Get("timestamp")!, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
            .ShouldBeInRange(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        problem.Headers["Cache-Control"].ShouldBe("no-store");
    }

    [Fact]
    public async Task Body_OmitsNulls()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException());

        var problem = await app.GetProblemAsync();

        problem.Has("errors").ShouldBeFalse();
        problem.Has("debug").ShouldBeFalse();
        problem.Text.ShouldNotContain("null");
    }

    [Fact]
    public async Task Body_ClearsWhatTheFailedRequestAlreadySet()
    {
        using var app = await TestApp.StartAsync(context =>
        {
            context.Response.StatusCode = 201;
            context.Response.Headers["X-Partial"] = "1";
            throw new InvalidOperationException();
        });

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        problem.Headers.ContainsKey("X-Partial").ShouldBeFalse();
    }

    [Fact]
    public async Task Body_KeepsUzbekTextReadable()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException());

        var problem = await app.GetProblemAsync("/x?culture=uz-Cyrl");
        var latin = await app.GetProblemAsync("/x?culture=uz");

        problem.Text.ShouldNotContain("\\u");
        latin.Text.ShouldNotContain("\\u");
    }

    [Fact]
    public async Task Instance_IsEncodedAndIncludesPathBase()
    {
        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            beforeFaultTrace: pipeline => pipeline.Use((context, next) =>
            {
                context.Request.PathBase = "/base";
                return next();
            }));

        var problem = await app.GetProblemAsync("/a%20b");

        problem.Get("instance").ShouldBe("/base/a%20b");
    }

    [Theory]
    [InlineData("https://errors.example.test/app/")]
    [InlineData("https://errors.example.test/app")]
    public async Task Type_UsesTheConfiguredBase(string baseUri)
    {
        using var app = await TestApp.ThrowingAsync(
            new Driver.OracleException("ORA-02292: integrity constraint (APP.FK_ORDER) violated", 2292),
            options => options.TypeBaseUri = baseUri);

        (await app.GetProblemAsync()).Get("type").ShouldBe("https://errors.example.test/app/has-dependents");
    }

    [Fact]
    public async Task Status503_SendsDefaultRetryAfter()
    {
        using var app = await TestApp.ThrowingAsync(new Driver.OracleException("ORA-03113: end-of-file on communication channel", 3113));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(503);
        problem.Headers["Retry-After"].ShouldBe("5");
    }

    [Fact]
    public async Task Status503_UsesConfiguredRetryAfter()
    {
        using var app = await TestApp.ThrowingAsync(
            new Driver.OracleException("ORA-03113: end-of-file on communication channel", 3113),
            options => options.RetryAfterSeconds = 30);

        (await app.GetProblemAsync()).Headers["Retry-After"].ShouldBe("30");
    }

    [Fact]
    public async Task OtherStatuses_SendNoRetryAfter()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException());

        (await app.GetProblemAsync()).Headers.ContainsKey("Retry-After").ShouldBeFalse();
    }

    [Fact]
    public async Task DetailExposure_DefaultInProduction_HasNoDebug()
    {
        using var app = await TestApp.ThrowingAsync(new Driver.OracleException(TechnicalMessage, 1400));

        var problem = await app.GetProblemAsync();

        problem.Has("debug").ShouldBeFalse();
        problem.Text.ShouldNotContain("ORA-");
    }

    [Fact]
    public async Task DetailExposure_DefaultInDevelopment_HasDebugBlock()
    {
        using var app = await TestApp.ThrowingAsync(new Driver.OracleException(TechnicalMessage, 1400), environment: "Development");

        var debug = (await app.GetProblemAsync()).Json.GetProperty("debug");

        debug.GetProperty("provider").GetString().ShouldBe("Oracle");
        debug.GetProperty("nativeCode").GetString().ShouldBe("ORA-01400");
        debug.GetProperty("message").GetString().ShouldBe(TechnicalMessage);
        debug.GetProperty("origin").GetString().ShouldBe("APP.ORDER_REQ:1749");
        debug.GetProperty("entry").GetString().ShouldBe("APP.ORDER_PACK:775");
        debug.GetProperty("details").EnumerateObject().ShouldNotBeEmpty();

        var segment = debug.GetProperty("chain").EnumerateArray().Single();
        segment.GetProperty("nativeCode").GetString().ShouldBe("ORA-01400");
        segment.GetProperty("frames").EnumerateArray().Select(frame => frame.GetString()).ShouldContain("APP.ORDER_REQ:1749");

        debug.GetProperty("dotnet").EnumerateArray().First().GetString()!.ShouldContain("OracleException");
    }

    [Fact]
    public async Task DetailExposure_NonDatabaseError_HasMessageAndStackOnly()
    {
        using var app = await TestApp.ThrowingAsync(
            new InvalidOperationException("secret detail"),
            options => options.DetailExposure = DetailExposure.Always);

        var debug = (await app.GetProblemAsync()).Json.GetProperty("debug");

        debug.GetProperty("message").GetString().ShouldBe("secret detail");
        debug.TryGetProperty("provider", out _).ShouldBeFalse();
        debug.TryGetProperty("chain", out _).ShouldBeFalse();
        debug.TryGetProperty("details", out _).ShouldBeFalse();
        debug.GetProperty("dotnet").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task DetailExposure_NeverInDevelopment_HasNoDebug()
    {
        using var app = await TestApp.ThrowingAsync(
            new InvalidOperationException(),
            options => options.DetailExposure = DetailExposure.Never,
            "Development");

        (await app.GetProblemAsync()).Has("debug").ShouldBeFalse();
    }

    [Fact]
    public async Task DetailExposure_Predicate_NarrowsAlways()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException(), options =>
        {
            options.DetailExposure = DetailExposure.Always;
            options.DetailExposurePredicate = context => context.Request.Query.ContainsKey("dev");
        });

        (await app.GetProblemAsync("/x?dev=1")).Has("debug").ShouldBeTrue();
        (await app.GetProblemAsync("/x")).Has("debug").ShouldBeFalse();
    }

    [Fact]
    public async Task DetailExposure_Predicate_CantWidenNever()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException(), options =>
        {
            options.DetailExposure = DetailExposure.Never;
            options.DetailExposurePredicate = _ => true;
        });

        (await app.GetProblemAsync()).Has("debug").ShouldBeFalse();
    }

    [Fact]
    public async Task DetailExposure_FailingPredicate_HidesDebugAndLogs()
    {
        using var app = await TestApp.ThrowingAsync(new InvalidOperationException(), options =>
        {
            options.DetailExposure = DetailExposure.Always;
            options.DetailExposurePredicate = _ => throw new NotSupportedException("predicate failure");
        });

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        problem.Has("debug").ShouldBeFalse();
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Exception is NotSupportedException);
    }

    [Fact]
    public async Task DetailExposure_ReloadsWithoutRestart()
    {
        var monitor = new TestOptionsMonitor(new FaultTraceAspNetCoreOptions { DetailExposure = DetailExposure.Never });
        using var app = await TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            services: services => services.AddSingleton<IOptionsMonitor<FaultTraceAspNetCoreOptions>>(monitor));

        (await app.GetProblemAsync()).Has("debug").ShouldBeFalse();

        monitor.CurrentValue = new FaultTraceAspNetCoreOptions { DetailExposure = DetailExposure.Always };

        (await app.GetProblemAsync()).Has("debug").ShouldBeTrue();
    }

    [Fact]
    public async Task Enricher_AddsFields()
    {
        using var app = await StartEnrichedAsync(new DelegateEnricher((_, fault, fields) =>
        {
            fields["tenant"] = "north";
            fields["limits"] = new { MaxItems = 3, Note = (string?)null };
            fields["skipped"] = null;
            fields["echo"] = fault.Code;
        }));

        var problem = await app.GetProblemAsync();

        problem.Get("tenant").ShouldBe("north");
        problem.Get("echo").ShouldBe("unexpected");
        problem.Json.GetProperty("limits").GetProperty("maxItems").GetInt32().ShouldBe(3);
        problem.Json.GetProperty("limits").TryGetProperty("note", out _).ShouldBeFalse();
        problem.Has("skipped").ShouldBeFalse();
    }

    [Fact]
    public async Task Enricher_CantReplaceCoreFields()
    {
        using var app = await StartEnrichedAsync(new DelegateEnricher((_, _, fields) =>
        {
            fields["detail"] = "replaced";
            fields["ERRORID"] = "replaced";
            fields["debug"] = "replaced";
        }));

        var problem = await app.GetProblemAsync();

        problem.Text.ShouldNotContain("replaced");
        problem.Get("errorId")!.Length.ShouldBe(9);
    }

    [Fact]
    public async Task Enricher_Failure_KeepsTheOtherFieldsAndLogs()
    {
        using var app = await StartEnrichedAsync(
            new DelegateEnricher((_, _, fields) => fields["first"] = 1),
            new DelegateEnricher((_, _, _) => throw new NotSupportedException("enricher failure")),
            new DelegateEnricher((_, _, fields) => fields["last"] = 2));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        problem.Get("code").ShouldBe(FaultCodes.Unexpected);
        problem.Json.GetProperty("first").GetInt32().ShouldBe(1);
        problem.Json.GetProperty("last").GetInt32().ShouldBe(2);
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Exception is NotSupportedException);
    }

    [Fact]
    public async Task Enricher_UnserializableValue_IsLeftOutAndLogged()
    {
        var cycle = new Node();
        cycle.Next = cycle;

        using var app = await StartEnrichedAsync(new DelegateEnricher((_, _, fields) =>
        {
            fields["bad"] = cycle;
            fields["good"] = "kept";
        }));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        problem.Has("bad").ShouldBeFalse();
        problem.Get("good").ShouldBe("kept");
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Exception is JsonException);
    }

    private static Task<TestApp> StartEnrichedAsync(params IProblemEnricher[] enrichers)
    {
        return TestApp.StartAsync(
            _ => throw new InvalidOperationException(),
            services: services =>
            {
                foreach (var enricher in enrichers)
                {
                    services.AddSingleton(enricher);
                }
            });
    }

    private sealed class Node
    {
        public Node? Next { get; set; }
    }

    private sealed class DelegateEnricher : IProblemEnricher
    {
        private readonly Action<HttpContext, Fault, IDictionary<string, object?>> _enrich;

        public DelegateEnricher(Action<HttpContext, Fault, IDictionary<string, object?>> enrich)
        {
            _enrich = enrich;
        }

        public void Enrich(HttpContext context, Fault fault, IDictionary<string, object?> extensions) =>
            _enrich(context, fault, extensions);
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<FaultTraceAspNetCoreOptions>
    {
        public TestOptionsMonitor(FaultTraceAspNetCoreOptions value)
        {
            CurrentValue = value;
        }

        public FaultTraceAspNetCoreOptions CurrentValue { get; set; }

        public FaultTraceAspNetCoreOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<FaultTraceAspNetCoreOptions, string?> listener) => throw new NotSupportedException();
    }
}
