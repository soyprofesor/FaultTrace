using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Logging;
using FaultTrace.Core.Model;
using FaultTrace.Core.Options;
using FaultTrace.Core.Translation;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace FaultTrace.Core.Tests.Translation;

public class FaultTranslatorTests
{
    private readonly CapturingLogger _logger = new();

    [Fact]
    public void Translate_UnknownException_IsGenericUnexpected()
    {
        var exception = new InvalidOperationException("secret table name");

        var fault = Create().Translate(exception, "ru");

        fault.Category.ShouldBe(FaultCategory.Internal);
        fault.Code.ShouldBe(FaultCodes.Unexpected);
        fault.Status.ShouldBe(500);
        fault.UserMessage.ShouldBe("Произошла непредвиденная ошибка. Обратитесь в службу поддержки и укажите код ошибки.");
        fault.UserMessage.ShouldNotContain("secret");
        fault.ErrorId.Length.ShouldBe(9);
        fault.Provider.ShouldBeNull();
    }

    [Fact]
    public void Translate_UsesTheFirstProviderThatRecognizesTheException()
    {
        var fault = Create(providers:
        [
            new FakeProvider(context => null),
            new FakeProvider(context => Business(context, "first")),
            new FakeProvider(context => Business(context, "second")),
        ]).Translate(new InvalidOperationException());

        fault.UserMessage.ShouldBe("first");
    }

    [Fact]
    public void Translate_PassesResolvedCultureAndIdsToProvider()
    {
        FaultContext? seen = null;
        var fault = Create(providers:
        [
            new FakeProvider(context =>
            {
                seen = context;
                return Business(context, "text");
            }),
        ]).Translate(new InvalidOperationException(), "uz-UZ");

        seen!.Culture.ShouldBe("uz-Latn");
        seen.ErrorId.ShouldBe(fault.ErrorId);
    }

    [Fact]
    public void Translate_FailingProvider_IsLoggedAndTheNextOneIsUsed()
    {
        var fault = Create(providers:
        [
            new FakeProvider(context => throw new InvalidOperationException("provider failure")),
            new FakeProvider(context => Business(context, "second")),
        ]).Translate(new InvalidOperationException());

        fault.UserMessage.ShouldBe("second");
        _logger.Entries.ShouldContain(entry => entry.Exception.Message == "provider failure" && entry.Level == LogLevel.Error);
    }

    [Fact]
    public void Translate_FailingProviderAlone_GivesUnexpected()
    {
        var fault = Create(providers:
        [
            new FakeProvider(context => throw new InvalidOperationException("provider failure")),
        ]).Translate(new ArgumentException());

        fault.Code.ShouldBe(FaultCodes.Unexpected);
    }

    [Fact]
    public void Translate_FailingMessageProvider_GivesHardCodedFallback()
    {
        var translator = new FaultTranslator(
            [],
            [],
            new BrokenMessageProvider(),
            _logger);

        var fault = translator.Translate(new InvalidOperationException());

        fault.Code.ShouldBe(FaultCodes.Unexpected);
        fault.Status.ShouldBe(500);
        fault.UserMessage.ShouldBe("An unexpected error occurred.");
        fault.ErrorId.Length.ShouldBe(9);
    }

    [Fact]
    public void Translate_FailingLogger_StillReturnsFault()
    {
        _logger.ThrowOnLog = true;

        Create().Translate(new InvalidOperationException()).Code.ShouldBe(FaultCodes.Unexpected);
    }

    [Fact]
    public void Translate_NullException_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Create().Translate(null!));
    }

    [Fact]
    public void Translate_LogsOnceAndReusesErrorIdWhenRethrown()
    {
        var translator = Create();
        var exception = new InvalidOperationException();

        var local = translator.Translate(exception);
        var global = translator.Translate(exception);

        global.ErrorId.ShouldBe(local.ErrorId);
        _logger.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public void Translate_ReusesErrorIdWhenWrappedInAnotherException()
    {
        var translator = Create();
        var inner = new InvalidOperationException();
        var local = translator.Translate(inner);

        var global = translator.Translate(new ApplicationException("outer", inner));

        global.ErrorId.ShouldBe(local.ErrorId);
        _logger.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public void Translate_DifferentExceptionsGetDifferentIds()
    {
        var translator = Create();

        translator.Translate(new InvalidOperationException()).ErrorId
            .ShouldNotBe(translator.Translate(new InvalidOperationException()).ErrorId);
    }

    [Fact]
    public void Translate_LogsStructuredProperties()
    {
        var exception = new InvalidOperationException();
        var details = new ProviderFaultDetails(
            "Oracle",
            "ORA-01400",
            new FaultFrame("APP.ORDER_REQ", 1749),
            new FaultFrame("APP.ORDER_PACK", 775),
            [new FaultSegment("ORA-01400", "text", [new FaultFrame("APP.ORDER_REQ", 1749), new FaultFrame(null, 1)])],
            new Dictionary<string, string> { ["column"] = "CODE" },
            "raw");

        var fault = Create(providers:
        [
            new FakeProvider(context => new Fault(context.ErrorId, FaultCategory.Validation, FaultCodes.RequiredField, "text", 400, provider: details)),
        ]).Translate(exception, "ru");

        var entry = _logger.Entries.Single();
        entry.Exception.ShouldBeSameAs(exception);
        entry.Properties["ErrorId"].ShouldBe(fault.ErrorId);
        entry.Properties["Code"].ShouldBe(FaultCodes.RequiredField);
        entry.Properties["Status"].ShouldBe(400);
        entry.Properties["Category"].ShouldBe(FaultCategory.Validation);
        entry.Properties["NativeCode"].ShouldBe("ORA-01400");
        entry.Properties["Origin"].ShouldBe("APP.ORDER_REQ:1749");
        entry.Properties["Entry"].ShouldBe("APP.ORDER_PACK:775");
        entry.Properties["RawMessage"].ShouldBe("raw");
        entry.Properties["Culture"].ShouldBe("ru");
        entry.Properties["Unmapped"].ShouldBe(false);
        ((string[])entry.Properties["Chain"]).ShouldBe(["ORA-01400 [APP.ORDER_REQ:1749 > anonymous:1]"]);
    }

    [Fact]
    public void Translate_LogsUnmappedMarker()
    {
        Create(providers:
        [
            new FakeProvider(context => new Fault(
                context.ErrorId,
                FaultCategory.Internal,
                FaultCodes.Unexpected,
                "text",
                500,
                provider: new ProviderFaultDetails("Oracle", isUnmapped: true))),
        ]).Translate(new InvalidOperationException());

        _logger.Entries.Single().Properties["Unmapped"].ShouldBe(true);
    }

    [Theory]
    [InlineData(FaultCategory.Internal, LogLevel.Error)]
    [InlineData(FaultCategory.Business, LogLevel.Warning)]
    [InlineData(FaultCategory.Validation, LogLevel.Warning)]
    [InlineData(FaultCategory.Conflict, LogLevel.Warning)]
    [InlineData(FaultCategory.Transient, LogLevel.Warning)]
    [InlineData(FaultCategory.Timeout, LogLevel.Warning)]
    [InlineData(FaultCategory.Unauthorized, LogLevel.Information)]
    [InlineData(FaultCategory.Forbidden, LogLevel.Information)]
    [InlineData(FaultCategory.NotFound, LogLevel.Information)]
    [InlineData(FaultCategory.ClientClosed, LogLevel.Information)]
    public void GetLevel_FollowsTheCategory(FaultCategory category, LogLevel expected)
    {
        FaultLogger.GetLevel(category).ShouldBe(expected);
    }

    [Fact]
    public void Translate_UsesCurrentActivityAsTraceId()
    {
        var activity = new Activity("test");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        try
        {
            var fault = Create().Translate(new InvalidOperationException());

            fault.TraceId.ShouldBe(activity.TraceId.ToHexString());
        }
        finally
        {
            activity.Stop();
        }
    }

    [Fact]
    public void Translate_WithoutActivity_HasNoTraceId()
    {
        Activity.Current = null;

        Create().Translate(new InvalidOperationException()).TraceId.ShouldBeNull();
    }

    [Fact]
    public void Translate_RunsHooksOnce()
    {
        var hook = new FakeHook();
        var translator = Create(hooks: [hook]);
        var exception = new InvalidOperationException();

        var fault = translator.Translate(exception);
        translator.Translate(exception);

        hook.Calls.Single().ShouldBe((fault, exception));
    }

    [Fact]
    public void Translate_FailingHook_IsLoggedAndOthersStillRun()
    {
        var hook = new FakeHook();
        var fault = Create(hooks: [new FakeHook(new InvalidOperationException("hook failure")), hook])
            .Translate(new InvalidOperationException());

        fault.Code.ShouldBe(FaultCodes.Unexpected);
        hook.Calls.Count.ShouldBe(1);
        _logger.Entries.ShouldContain(entry => entry.Exception.Message == "hook failure");
    }

    [Fact]
    public void Translate_TimeoutAndCancellation_AreTransientTimeouts()
    {
        foreach (var exception in new Exception[]
        {
            new TimeoutException(),
            new OperationCanceledException(),
            new AggregateException(new TaskCanceledException()),
            new InvalidOperationException("wrapper", new TimeoutException()),
        })
        {
            var fault = Create().Translate(exception);

            fault.Category.ShouldBe(FaultCategory.Timeout);
            fault.Code.ShouldBe(FaultCodes.Timeout);
            fault.Status.ShouldBe(504);
            fault.IsTransient.ShouldBeTrue();
        }
    }

    [Fact]
    public void Translate_ProviderWinsOverTheTimeoutRule()
    {
        var fault = Create(providers: [new FakeProvider(context => Business(context, "first"))])
            .Translate(new TimeoutException());

        fault.Code.ShouldBe(FaultCodes.Business);
    }

    [Fact]
    public void Translate_IoFailure_IsUnexpectedUnlessTheRequestWasAborted()
    {
        Create().Translate(new IOException()).Code.ShouldBe(FaultCodes.Unexpected);

        Create(requests: new FakeRequests(() => new FaultRequest(null, null, null, isAborted: true)))
            .Translate(new IOException()).Code.ShouldBe(FaultCodes.ClientClosed);
    }

    [Fact]
    public void Translate_TimeoutFromProviderInAbortedRequest_IsClientClosedAndKeepsDetails()
    {
        var details = new ProviderFaultDetails("Fake", "F-1");
        var provider = new FakeProvider(context =>
            new Fault(context.ErrorId, FaultCategory.Timeout, FaultCodes.Timeout, "text", 504, isTransient: true, context.TraceId, provider: details));

        var fault = Create([provider], requests: new FakeRequests(() => new FaultRequest(null, null, null, "trace", isAborted: true)))
            .Translate(new InvalidOperationException(), "ru");

        fault.Category.ShouldBe(FaultCategory.ClientClosed);
        fault.Code.ShouldBe(FaultCodes.ClientClosed);
        fault.Status.ShouldBe(499);
        fault.IsTransient.ShouldBeFalse();
        fault.UserMessage.ShouldNotBe("text");
        fault.TraceId.ShouldBe("trace");
        fault.Provider.ShouldBeSameAs(details);
        _logger.Entries.Single().Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public void Translate_BugInAbortedRequest_StaysUnexpected()
    {
        Create(requests: new FakeRequests(() => new FaultRequest(null, null, null, isAborted: true)))
            .Translate(new InvalidOperationException()).Code.ShouldBe(FaultCodes.Unexpected);
    }

    [Fact]
    public void Translate_WithRequest_LogsItsContext()
    {
        Create(requests: new FakeRequests(() => new FaultRequest("/api/order/add", "POST", "user-1")))
            .Translate(new InvalidOperationException());

        var entry = _logger.Entries.Single();
        entry.Properties["Endpoint"].ShouldBe("/api/order/add");
        entry.Properties["Method"].ShouldBe("POST");
        entry.Properties["User"].ShouldBe("user-1");
    }

    [Fact]
    public void Translate_WithoutRequest_LogsNullContext()
    {
        Create().Translate(new InvalidOperationException());

        _logger.Entries.Single().Properties["Endpoint"].ShouldBeNull();
    }

    [Fact]
    public void Translate_FailingRequestAccessor_IsLoggedAndTheFaultStillComes()
    {
        var fault = Create(requests: new FakeRequests(() => throw new InvalidOperationException("accessor failure")))
            .Translate(new InvalidOperationException());

        fault.Code.ShouldBe(FaultCodes.Unexpected);
        _logger.Entries.ShouldContain(entry => entry.Exception.Message == "accessor failure");
    }

    [Fact]
    public void FactoryFault_UsesTheLocalizedMessageAndLogsOnce()
    {
        var hook = new FakeHook();

        var fault = Create(hooks: [hook]).Create(FaultCategory.NotFound, FaultCodes.NotFound, 404, "ru");

        fault.Status.ShouldBe(404);
        fault.Code.ShouldBe(FaultCodes.NotFound);
        fault.UserMessage.ShouldBe("Запрошенные данные не найдены.");
        fault.ErrorId.Length.ShouldBe(9);
        _logger.Entries.Count.ShouldBe(1);
        _logger.Entries[0].Level.ShouldBe(LogLevel.Information);
        hook.Calls.Single().Exception.ShouldBeNull();
    }

    [Fact]
    public void FactoryFault_KeepsValidationErrors()
    {
        var errors = new Dictionary<string, IReadOnlyList<string>> { ["amount"] = ["Required"] };

        var fault = Create().Create(FaultCategory.Validation, FaultCodes.Validation, 400, errors: errors);

        fault.Errors!["amount"].ShouldBe(["Required"]);
        _logger.Entries[0].Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public void FactoryFault_ShowsItsOwnDetail()
    {
        var fault = Create().Create(FaultCategory.Business, "order_closed", 400, detail: "Order is closed");

        fault.UserMessage.ShouldBe("Order is closed");
    }

    [Fact]
    public void FactoryFault_WithAnOwnCode_UsesTheTextOfItsCategory()
    {
        var fault = Create().Create(FaultCategory.NotFound, "order_missing", 404, "ru");

        fault.UserMessage.ShouldBe("Запрошенные данные не найдены.");
    }

    [Fact]
    public void FactoryFault_WithInvalidArguments_FallsBackAndLogs()
    {
        var fault = Create().Create(FaultCategory.NotFound, FaultCodes.NotFound, 200);

        fault.Code.ShouldBe(FaultCodes.Unexpected);
        fault.Status.ShouldBe(500);
        _logger.Entries.ShouldContain(entry => entry.Exception is ArgumentOutOfRangeException);
    }

    private FaultTranslator Create(IFaultProvider[]? providers = null, IFaultHook[]? hooks = null, IFaultRequestAccessor? requests = null)
    {
        return new FaultTranslator(
            providers ?? [],
            hooks ?? [],
            new DefaultFaultMessageProvider(Microsoft.Extensions.Options.Options.Create(new FaultTraceOptions())),
            _logger,
            requests);
    }

    private static Fault Business(FaultContext context, string text) =>
        new(context.ErrorId, FaultCategory.Business, FaultCodes.Business, text, 400, traceId: context.TraceId);

    private sealed class FakeProvider : IFaultProvider
    {
        private readonly Func<FaultContext, Fault?> _translate;

        public FakeProvider(Func<FaultContext, Fault?> translate)
        {
            _translate = translate;
        }

        public Fault? TryTranslate(Exception exception, FaultContext context) => _translate(context);
    }

    private sealed class FakeHook : IFaultHook
    {
        private readonly Exception? _failure;

        public FakeHook(Exception? failure = null)
        {
            _failure = failure;
        }

        public List<(Fault Fault, Exception? Exception)> Calls { get; } = [];

        public void OnFault(Fault fault, Exception? exception)
        {
            if (_failure != null)
            {
                throw _failure;
            }

            Calls.Add((fault, exception));
        }
    }

    private sealed class BrokenMessageProvider : IFaultMessageProvider
    {
        public string ResolveCulture(string? culture) => throw new InvalidOperationException("Message failure");

        public string? FindCulture(string? culture) => throw new InvalidOperationException("Message failure");

        public FaultMessage? Find(string code, string culture) => throw new InvalidOperationException("Message failure");

        public FaultMessage Get(string code, string culture) => throw new InvalidOperationException("Message failure");
    }

    private sealed class FakeRequests : IFaultRequestAccessor
    {
        private readonly Func<FaultRequest?> _get;

        public FakeRequests(Func<FaultRequest?> get)
        {
            _get = get;
        }

        public FaultRequest? GetCurrent() => _get();
    }
}
