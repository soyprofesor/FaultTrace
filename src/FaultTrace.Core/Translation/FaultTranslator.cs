using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.ErrorIds;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Logging;
using FaultTrace.Core.Model;
using Microsoft.Extensions.Logging;

namespace FaultTrace.Core.Translation;

internal sealed class FaultTranslator : IFaultTranslator, IFaultFactory
{
    private const string HandledFaultKey = "FaultTrace.Fault";
    private const string FallbackMessage = "An unexpected error occurred.";
    private const int MaxDepth = 16;

    private readonly IFaultProvider[] _providers;
    private readonly IFaultHook[] _hooks;
    private readonly IFaultMessageProvider _messages;
    private readonly ILogger _logger;
    private readonly IFaultRequestAccessor? _requests;

    public FaultTranslator(
        IEnumerable<IFaultProvider> providers,
        IEnumerable<IFaultHook> hooks,
        IFaultMessageProvider messages,
        ILogger logger,
        IFaultRequestAccessor? requests = null)
    {
        _providers = providers.ToArray();
        _hooks = hooks.ToArray();
        _messages = messages;
        _logger = logger;
        _requests = requests;
    }

    public Fault Translate(Exception exception, string? culture = null)
    {
        if (exception == null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        // Handled and logged in a local catch, then rethrown: keep the same ErrorId and don't log twice.
        var handled = FindHandled(exception);
        if (handled != null)
        {
            return handled;
        }

        Fault fault;
        string? resolvedCulture = null;
        var request = GetRequest();
        try
        {
            resolvedCulture = _messages.ResolveCulture(culture ?? request?.Culture);
            fault = Create(exception, resolvedCulture, request);
        }
        catch (Exception failure)
        {
            LogFailure(failure);
            fault = CreateFallback();
        }

        Remember(exception, fault);
        Log(fault, exception, resolvedCulture, request);
        RunHooks(fault, exception);

        return fault;
    }

    public Fault Create(
        FaultCategory category,
        string code,
        int status,
        string? culture = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null,
        string? detail = null)
    {
        Fault fault;
        string? resolvedCulture = null;
        var request = GetRequest();
        try
        {
            resolvedCulture = _messages.ResolveCulture(culture ?? request?.Culture);
            var text = string.IsNullOrWhiteSpace(detail)
                ? (_messages.Find(code, resolvedCulture) ?? _messages.Get(FaultCodes.ForCategory(category), resolvedCulture)).Detail
                : detail!;
            var isTransient = category == FaultCategory.Transient || category == FaultCategory.Timeout;
            fault = new Fault(ErrorIdGenerator.NewId(), category, code, text, status, isTransient, traceId: GetTraceId() ?? request?.TraceId, errors: errors);
        }
        catch (Exception failure)
        {
            LogFailure(failure);
            fault = CreateFallback();
        }

        Log(fault, null, resolvedCulture, request);
        RunHooks(fault, null);

        return fault;
    }

    private Fault Create(Exception exception, string culture, FaultRequest? request)
    {
        var errorId = ErrorIdGenerator.NewId();
        var traceId = GetTraceId() ?? request?.TraceId;
        var fault = FromProviders(exception, new FaultContext(errorId, traceId, culture))
            ?? FromException(exception, errorId, traceId, culture, request);

        return request?.IsAborted == true && fault.Category == FaultCategory.Timeout
            ? ToClientClosed(fault, culture)
            : fault;
    }

    private Fault? FromProviders(Exception exception, FaultContext context)
    {
        foreach (var provider in _providers)
        {
            try
            {
                var fault = provider.TryTranslate(exception, context);
                if (fault != null)
                {
                    return fault;
                }
            }
            catch (Exception failure)
            {
                LogFailure(failure);
            }
        }

        return null;
    }

    private Fault FromException(Exception exception, string errorId, string? traceId, string culture, FaultRequest? request)
    {
        if (IsTimeout(exception, request?.IsAborted == true, 0))
        {
            var timeout = _messages.Get(FaultCodes.Timeout, culture);
            return new Fault(errorId, FaultCategory.Timeout, FaultCodes.Timeout, timeout.Detail, 504, isTransient: true, traceId: traceId);
        }

        var message = _messages.Get(FaultCodes.Unexpected, culture);
        return new Fault(errorId, FaultCategory.Internal, FaultCodes.Unexpected, message.Detail, 500, traceId: traceId);
    }

    private Fault ToClientClosed(Fault fault, string culture)
    {
        var message = _messages.Get(FaultCodes.ClientClosed, culture);
        return new Fault(fault.ErrorId, FaultCategory.ClientClosed, FaultCodes.ClientClosed, message.Detail, 499, traceId: fault.TraceId, provider: fault.Provider);
    }

    // An I/O failure only counts once the caller has gone: reading from or writing to a closed connection.
    private static bool IsTimeout(Exception exception, bool isAborted, int depth)
    {
        if (exception is OperationCanceledException || exception is TimeoutException || (isAborted && exception is IOException))
        {
            return true;
        }

        if (depth == MaxDepth)
        {
            return false;
        }

        if (exception is AggregateException aggregate)
        {
            return aggregate.InnerExceptions.Any(inner => IsTimeout(inner, isAborted, depth + 1));
        }

        return exception.InnerException != null && IsTimeout(exception.InnerException, isAborted, depth + 1);
    }

    private static Fault CreateFallback() =>
        new Fault(ErrorIdGenerator.NewId(), FaultCategory.Internal, FaultCodes.Unexpected, FallbackMessage, 500);

    private FaultRequest? GetRequest()
    {
        try
        {
            return _requests?.GetCurrent();
        }
        catch (Exception failure)
        {
            LogFailure(failure);
            return null;
        }
    }

    private static string? GetTraceId()
    {
        var activity = Activity.Current;
        if (activity == null)
        {
            return null;
        }

        return activity.IdFormat == ActivityIdFormat.W3C ? activity.TraceId.ToHexString() : activity.RootId;
    }

    private static Fault? FindHandled(Exception exception)
    {
        var current = exception;
        for (var depth = 0; current != null && depth <= MaxDepth; depth++)
        {
            if (current.Data[HandledFaultKey] is Fault fault)
            {
                return fault;
            }

            current = current.InnerException;
        }

        return null;
    }

    private void Remember(Exception exception, Fault fault)
    {
        try
        {
            exception.Data[HandledFaultKey] = fault;
        }
        catch (Exception failure)
        {
            LogFailure(failure);
        }
    }

    private void Log(Fault fault, Exception? exception, string? culture, FaultRequest? request)
    {
        try
        {
            FaultLogger.Log(_logger, fault, exception, culture, request);
        }
        catch (Exception)
        {
            // The logger itself failed, so there is nowhere left to report this. The caller still gets the fault.
        }
    }

    private void RunHooks(Fault fault, Exception? exception)
    {
        foreach (var hook in _hooks)
        {
            try
            {
                hook.OnFault(fault, exception);
            }
            catch (Exception failure)
            {
                LogFailure(failure);
            }
        }
    }

    private void LogFailure(Exception failure)
    {
        try
        {
            _logger.LogError(failure, "FaultTrace failed while handling an error");
        }
        catch (Exception)
        {
            // The logger itself failed, so there is nowhere left to report this.
        }
    }
}
