using System;
using System.Collections.Generic;
using System.Linq;
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
using Driver = global::Oracle.ManagedDataAccess.Client;

namespace FaultTrace.AspNetCore.Tests.Handling;

public class ProjectExceptionTests
{
    [Fact]
    public async Task InterfaceException_GetsItsOwnStatusCodeAndDetail()
    {
        using var app = await TestApp.ThrowingAsync(new OrderException(409, "order_closed", "Order is already closed"));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(409);
        problem.Get("code").ShouldBe("order_closed");
        problem.Get("detail").ShouldBe("Order is already closed");
        problem.Get("type").ShouldBe("https://soyprofesor.github.io/FaultTrace/errors/order-closed");
        app.Logs.Faults.Single().Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task MappedException_NeedsNoChangeToTheClass()
    {
        using var app = await TestApp.ThrowingAsync(
            new PlainException("Not enough funds"),
            options => options.MapException<PlainException>(exception => new FaultProblem(422, "no_funds", exception.Message)));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(422);
        problem.Get("code").ShouldBe("no_funds");
        problem.Get("detail").ShouldBe("Not enough funds");
    }

    [Fact]
    public async Task UnknownCode_GetsTheTitleOfItsCategory()
    {
        using var app = await TestApp.ThrowingAsync(new OrderException(404, "order_missing", null));

        var problem = await app.GetProblemAsync("/x?culture=ru");

        problem.Get("title").ShouldBe("Не найдено");
        problem.Get("detail").ShouldBe("Запрошенные данные не найдены.");
    }

    [Fact]
    public async Task CustomCodeWithItsOwnMessages_UsesThem()
    {
        using var app = await TestApp.ThrowingAsync(
            new OrderException(400, "order_closed", null),
            options => options.SetMessage("en", "order_closed", "Order closed", "This order can't be changed."));

        var problem = await app.GetProblemAsync();

        problem.Get("title").ShouldBe("Order closed");
        problem.Get("detail").ShouldBe("This order can't be changed.");
    }

    [Fact]
    public async Task ExceptionWithErrors_ShowsThem()
    {
        var errors = new Dictionary<string, IReadOnlyList<string>> { ["amount"] = ["Too large"] };
        using var app = await TestApp.ThrowingAsync(new OrderException(400, "limits", "Check the limits", errors));

        var problem = await app.GetProblemAsync();

        problem.Json.GetProperty("errors").GetProperty("amount")[0].GetString().ShouldBe("Too large");
    }

    [Fact]
    public async Task Unavailable_SendsRetryAfter()
    {
        using var app = await TestApp.ThrowingAsync(new OrderException(503, "bank_down", "Try later"));

        var problem = await app.GetProblemAsync();

        problem.Headers["Retry-After"].ShouldBe("5");
    }

    [Fact]
    public async Task Mapping_WinsOverTheOracleProvider()
    {
        using var app = await TestApp.ThrowingAsync(
            new PlainException("Wrapped", new Driver.OracleException("ORA-00942: table or view does not exist", 942)),
            options => options.MapException<PlainException>(exception => new FaultProblem(400, "wrapped", exception.Message)));

        (await app.GetProblemAsync()).Get("code").ShouldBe("wrapped");
    }

    [Fact]
    public async Task InvalidStatus_IsLoggedAndBecomesUnexpected()
    {
        using var app = await TestApp.ThrowingAsync(new OrderException(200, "not_an_error", "Fine"));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(500);
        problem.Get("code").ShouldBe(FaultCodes.Unexpected);
        problem.Text.ShouldNotContain("Fine");
        app.Logs.FaultTraceEntries.ShouldContain(entry => entry.Exception is ArgumentOutOfRangeException);
    }

    [Fact]
    public async Task LocalTranslationThenRethrow_UsesTheRequestCultureAndLogsOnce()
    {
        using var app = await TestApp.StartAsync(context =>
        {
            var exception = new InvalidOperationException("local");
            context.RequestServices.GetRequiredService<IFaultTranslator>().Translate(exception);
            throw exception;
        });

        var problem = await app.GetProblemAsync("/x?culture=ru");

        problem.Get("title").ShouldBe("Непредвиденная ошибка");
        problem.Get("detail").ShouldBe("Произошла непредвиденная ошибка. Обратитесь в службу поддержки и укажите код ошибки.");
        var log = app.Logs.Faults.Single();
        log.Properties["ErrorId"].ShouldBe(problem.Get("errorId"));
        log.Properties["Culture"].ShouldBe("ru");
    }

    [Theory]
    [InlineData(413)]
    [InlineData(400)]
    [InlineData(431)]
    public async Task RejectedRequest_KeepsItsStatus(int status)
    {
        using var app = await TestApp.ThrowingAsync(CreateBadRequest(status));

        var problem = await app.GetProblemAsync();

        problem.Status.ShouldBe(status);
        problem.Get("code").ShouldBe(FaultCodes.Validation);
        app.Logs.Faults.Single().Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task RejectedRequestAfterTheClientLeft_IsClientClosed()
    {
        var exception = CreateBadRequest(400);
        using var app = await TestApp.StartAsync(
            _ => throw exception,
            beforeFaultTrace: pipeline => pipeline.Use((context, next) =>
            {
                context.RequestAborted = new CancellationToken(true);
                return next();
            }));

        (await app.GetProblemAsync()).Status.ShouldBe(499);
    }

    private static Exception CreateBadRequest(int status)
    {
#if NETCOREAPP3_1
        // Kestrel's exception has no public constructor on 3.1.
        var constructor = typeof(Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException)
            .GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 3);
        var reason = Activator.CreateInstance(constructor.GetParameters()[2].ParameterType);
        return (Exception)constructor.Invoke(["rejected", status, reason]);
#else
        return new BadHttpRequestException("rejected", status);
#endif
    }

    private sealed class PlainException : Exception
    {
        public PlainException(string message, Exception? inner = null)
            : base(message, inner)
        {
        }
    }

    private sealed class OrderException : Exception, IFaultException
    {
        public OrderException(int status, string code, string? detail, IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
        {
            Status = status;
            Code = code;
            Detail = detail;
            Errors = errors;
        }

        public int Status { get; }

        public string Code { get; }

        public string? Detail { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors { get; }
    }
}
