using System;
using System.Collections.Generic;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;
using FaultTrace.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FaultTrace.Core.Translation;

// Translates a project's own exceptions: a registered mapping first, then IFaultException.
internal sealed class ExceptionFaultProvider : IFaultProvider
{
    private const int MaxDepth = 16;

    private readonly Dictionary<Type, Func<Exception, FaultProblem?>> _mappings;
    private readonly IFaultMessageProvider _messages;
    private readonly ILogger _logger;

    public ExceptionFaultProvider(IOptions<FaultTraceOptions> options, IFaultMessageProvider messages, ILogger logger)
    {
        _mappings = options.Value.ExceptionMappings;
        _messages = messages;
        _logger = logger;
    }

    public Fault? TryTranslate(Exception exception, FaultContext context) => Find(exception, context, 0);

    internal static FaultCategory GetCategory(int status, bool hasErrors)
    {
        if (hasErrors)
        {
            return FaultCategory.Validation;
        }

        switch (status)
        {
            case 401:
                return FaultCategory.Unauthorized;
            case 403:
                return FaultCategory.Forbidden;
            case 404:
            case 410:
                return FaultCategory.NotFound;
            case 409:
                return FaultCategory.Conflict;
            case 499:
                return FaultCategory.ClientClosed;
            case 429:
            case 503:
                return FaultCategory.Transient;
            case 504:
                return FaultCategory.Timeout;
            default:
                return status < 500 ? FaultCategory.Business : FaultCategory.Internal;
        }
    }

    private Fault? Find(Exception? exception, FaultContext context, int depth)
    {
        if (exception == null || depth > MaxDepth)
        {
            return null;
        }

        var fault = FromMapping(exception, context) ?? FromInterface(exception, context);
        if (fault != null)
        {
            return fault;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                var found = Find(inner, context, depth + 1);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        return Find(exception.InnerException, context, depth + 1);
    }

    private Fault? FromMapping(Exception exception, FaultContext context)
    {
        // Walking up from the exact type means the most specific mapping wins.
        for (var type = exception.GetType(); type != null; type = type.BaseType)
        {
            if (!_mappings.TryGetValue(type, out var map))
            {
                continue;
            }

            try
            {
                var problem = map(exception);
                return problem == null ? null : Create(problem.Status, problem.Code, problem.Detail, problem.Errors, context);
            }
            catch (Exception failure)
            {
                _logger.LogError(failure, "FaultTrace exception mapping for {ExceptionType} failed", type.FullName);
                return null;
            }
        }

        return null;
    }

    private Fault? FromInterface(Exception exception, FaultContext context)
    {
        if (!(exception is IFaultException project))
        {
            return null;
        }

        try
        {
            return Create(project.Status, project.Code, project.Detail, project.Errors, context);
        }
        catch (Exception failure)
        {
            _logger.LogError(failure, "FaultTrace could not read IFaultException from {ExceptionType}", exception.GetType().FullName);
            return null;
        }
    }

    private Fault Create(int status, string code, string? detail, IReadOnlyDictionary<string, IReadOnlyList<string>>? errors, FaultContext context)
    {
        var category = GetCategory(status, errors != null && errors.Count > 0);
        var text = string.IsNullOrWhiteSpace(detail) ? GetGenericDetail(code, category, context.Culture) : detail!;

        return new Fault(
            context.ErrorId,
            category,
            code,
            text,
            status,
            isTransient: category == FaultCategory.Transient || category == FaultCategory.Timeout,
            traceId: context.TraceId,
            errors: errors);
    }

    private string GetGenericDetail(string code, FaultCategory category, string? culture)
    {
        var resolved = _messages.ResolveCulture(culture);
        return (_messages.Find(code, resolved) ?? _messages.Get(FaultCodes.ForCategory(category), resolved)).Detail;
    }
}
