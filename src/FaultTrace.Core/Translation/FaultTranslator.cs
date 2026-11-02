using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.ErrorIds;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Logging;
using FaultTrace.Core.Model;
using Microsoft.Extensions.Logging;

namespace FaultTrace.Core.Translation;

internal sealed class FaultTranslator : IFaultTranslator
{
    private const string HandledFaultKey = "FaultTrace.Fault";
    private const string FallbackMessage = "An unexpected error occurred.";
    private const int MaxDepth = 16;

    private readonly IFaultProvider[] _providers;
    private readonly IFaultHook[] _hooks;
    private readonly IFaultMessageProvider _messages;
    private readonly ILogger _logger;

    public FaultTranslator(
        IEnumerable<IFaultProvider> providers,
        IEnumerable<IFaultHook> hooks,
        IFaultMessageProvider messages,
        ILogger logger)
    {
        _providers = providers.ToArray();
        _hooks = hooks.ToArray();
        _messages = messages;
        _logger = logger;
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
        try
        {
            resolvedCulture = _messages.ResolveCulture(culture);
            fault = Create(exception, resolvedCulture);
        }
        catch (Exception failure)
        {
            LogFailure(failure);
            fault = CreateFallback();
        }

        Remember(exception, fault);
        Log(fault, exception, resolvedCulture);
        RunHooks(fault, exception);

        return fault;
    }

    private Fault Create(Exception exception, string culture)
    {
        var errorId = ErrorIdGenerator.NewId();
        var traceId = GetTraceId();
        var context = new FaultContext(errorId, traceId, culture);

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

        var message = _messages.Get(FaultCodes.Unexpected, culture);
        return new Fault(errorId, FaultCategory.Internal, FaultCodes.Unexpected, message.Detail, 500, traceId: traceId);
    }

    private static Fault CreateFallback() =>
        new Fault(ErrorIdGenerator.NewId(), FaultCategory.Internal, FaultCodes.Unexpected, FallbackMessage, 500);

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

    private void Log(Fault fault, Exception exception, string? culture)
    {
        try
        {
            FaultLogger.Log(_logger, fault, exception, culture);
        }
        catch (Exception)
        {
            // The logger itself failed, so there is nowhere left to report this. The caller still gets the fault.
        }
    }

    private void RunHooks(Fault fault, Exception exception)
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
