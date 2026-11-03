using System;
#if NETCOREAPP3_1
using System.Reflection;
#endif
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Http;

namespace FaultTrace.AspNetCore.Handling;

// A request the server rejected, such as a body over the size limit, is the client's error and not a bug.
internal sealed class BadRequestFaultProvider : IFaultProvider
{
    private const int MaxDepth = 16;

    private readonly IHttpContextAccessor _contexts;
    private readonly IFaultMessageProvider _messages;

    public BadRequestFaultProvider(IHttpContextAccessor contexts, IFaultMessageProvider messages)
    {
        _contexts = contexts;
        _messages = messages;
    }

    public Fault? TryTranslate(Exception exception, FaultContext context)
    {
        var status = FindStatus(exception);

        // A client that left in the middle of the body is not a bad request. The translator reports it as closed.
        if (status == null || status < 400 || status > 499 || _contexts.HttpContext?.RequestAborted.IsCancellationRequested == true)
        {
            return null;
        }

        var message = _messages.Get(FaultCodes.Validation, _messages.ResolveCulture(context.Culture));
        return new Fault(context.ErrorId, FaultCategory.Validation, FaultCodes.Validation, message.Detail, status.Value, traceId: context.TraceId);
    }

    private static int? FindStatus(Exception exception)
    {
        Exception? current = exception;
        for (var depth = 0; current != null && depth <= MaxDepth; depth++)
        {
            var status = ReadStatus(current);
            if (status != null)
            {
                return status;
            }

            current = current.InnerException;
        }

        return null;
    }

#if NETCOREAPP3_1
    // On 3.1 each server has its own exception type, and Kestrel's keeps the status internal.
    private static int? ReadStatus(Exception exception)
    {
        var type = exception.GetType();
        if (type.Name != "BadHttpRequestException" || type.Namespace?.StartsWith("Microsoft.AspNetCore.Server", StringComparison.Ordinal) != true)
        {
            return null;
        }

        var property = type.GetProperty("StatusCode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property?.GetValue(exception) as int? ?? StatusCodes.Status400BadRequest;
    }
#else
    private static int? ReadStatus(Exception exception) => (exception as BadHttpRequestException)?.StatusCode;
#endif
}
