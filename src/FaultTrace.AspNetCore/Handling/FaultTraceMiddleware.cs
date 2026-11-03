using System;
using System.Threading;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Culture;
using FaultTrace.AspNetCore.Response;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.ErrorIds;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FaultTrace.AspNetCore.Handling;

internal sealed class FaultTraceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IFaultTranslator _translator;
    private readonly FaultCultureResolver _cultures;
    private readonly ProblemResponseWriter _writer;
    private readonly ILogger _logger;

    public FaultTraceMiddleware(
        RequestDelegate next,
        IFaultTranslator translator,
        FaultCultureResolver cultures,
        ProblemResponseWriter writer,
        ILoggerFactory loggerFactory)
    {
        _next = next;
        _translator = translator;
        _cultures = cultures;
        _writer = writer;
        _logger = loggerFactory.CreateLogger("FaultTrace");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        Fault? fault = null;
        try
        {
            var culture = _cultures.Resolve(context);
            fault = _translator.Translate(exception, culture);

            if (context.Response.HasStarted)
            {
                // A client that leaves during streaming is routine, not an error.
                _logger.Log(
                    fault.Category == FaultCategory.ClientClosed ? LogLevel.Information : LogLevel.Error,
                    "Fault {ErrorId} happened after the response started, so the connection is aborted",
                    fault.ErrorId);
                context.Abort();
                return;
            }

            await _writer.WriteAsync(context, fault, exception, culture).ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            await WriteFallbackAsync(context, exception, fault, failure).ConfigureAwait(false);
        }
    }

    private async Task WriteFallbackAsync(HttpContext context, Exception exception, Fault? fault, Exception failure)
    {
        var errorId = fault?.ErrorId ?? ErrorIdGenerator.NewId();
        try
        {
            _logger.LogError(failure, "FaultTrace failed while handling fault {ErrorId}", errorId);
            if (fault == null)
            {
                _logger.LogError(exception, "Fault {ErrorId} is the original error that FaultTrace failed to translate", errorId);
            }
        }
        catch (Exception)
        {
            // The logger itself failed, so there is nowhere left to report this. The caller still gets a response.
        }

        try
        {
            if (context.Response.HasStarted)
            {
                context.Abort();
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = ProblemResponseWriter.ContentType;
            context.Response.Headers["Cache-Control"] = "no-store";

            var body = FallbackProblem.Create(errorId);
            context.Response.ContentLength = body.Length;
            await context.Response.Body.WriteAsync(body, 0, body.Length, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception fallbackFailure)
        {
            LogFallbackFailure(fallbackFailure, errorId);
            context.Abort();
        }
    }

    private void LogFallbackFailure(Exception failure, string errorId)
    {
        try
        {
            _logger.LogError(failure, "FaultTrace failed to write the fallback response for fault {ErrorId}", errorId);
        }
        catch (Exception)
        {
            // The logger itself failed, so there is nowhere left to report this.
        }
    }
}
