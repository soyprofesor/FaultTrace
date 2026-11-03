using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Configuration;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FaultTrace.AspNetCore.Response;

internal sealed class ProblemResponseWriter
{
    public const string ContentType = "application/problem+json";

    private readonly IFaultMessageProvider _messages;
    private readonly IOptionsMonitor<FaultTraceAspNetCoreOptions> _options;
    private readonly DetailExposureEvaluator _exposure;
    private readonly ILogger _logger;

    public ProblemResponseWriter(
        IFaultMessageProvider messages,
        IOptionsMonitor<FaultTraceAspNetCoreOptions> options,
        DetailExposureEvaluator exposure,
        ILoggerFactory loggerFactory)
    {
        _messages = messages;
        _options = options;
        _exposure = exposure;
        _logger = loggerFactory.CreateLogger("FaultTrace");
    }

    public async Task WriteAsync(HttpContext context, Fault fault, Exception? exception, string? culture)
    {
        var options = _options.CurrentValue;

        // Build the whole body first: a failure here must not leave a half-written response.
        var body = ProblemJsonWriter.Write(
            new ProblemJsonWriter.Problem(
                GetTypeUri(options.TypeBaseUri, fault.Code),
                GetTitle(fault, _messages.ResolveCulture(culture)),
                fault,
                context.Request.PathBase.Add(context.Request.Path).ToString(),
                fault.TraceId ?? context.TraceIdentifier,
                exception != null && _exposure.IsAllowed(context) ? exception : null),
            Enrich(context, fault),
            (key, failure) => _logger.LogError(failure, "FaultTrace left out problem field {Field} of fault {ErrorId}", key, fault.ErrorId));

        var response = context.Response;

        // After an exception the headers are from a request that failed. Without one they are deliberate, such as
        // WWW-Authenticate on a 401 or Allow on a 405, and must stay.
        if (exception != null)
        {
            response.Clear();
        }

        response.StatusCode = fault.Status;
        response.ContentType = ContentType;
        response.ContentLength = body.Length;
        response.Headers["Cache-Control"] = "no-store";

        if (fault.Status == StatusCodes.Status503ServiceUnavailable && options.RetryAfterSeconds > 0)
        {
            response.Headers["Retry-After"] = options.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        }

        // The request may already be aborted, and that token must not stop the response.
        await response.Body.WriteAsync(body, 0, body.Length, CancellationToken.None).ConfigureAwait(false);
    }

    // A project's own code usually has no texts, and the generic "unexpected" title would be wrong for it.
    private string GetTitle(Fault fault, string culture) =>
        (_messages.Find(fault.Code, culture) ?? _messages.Get(FaultCodes.ForCategory(fault.Category), culture)).Title;

    private static string GetTypeUri(string? baseUri, string code)
    {
        var start = string.IsNullOrWhiteSpace(baseUri) ? FaultTraceAspNetCoreOptions.DefaultTypeBaseUri : baseUri!;
        return start.TrimEnd('/') + "/" + code.Replace('_', '-');
    }

    private Dictionary<string, object?> Enrich(HttpContext context, Fault fault)
    {
        var extensions = new Dictionary<string, object?>();
        foreach (var enricher in context.RequestServices.GetServices<IProblemEnricher>())
        {
            try
            {
                enricher.Enrich(context, fault, extensions);
            }
            catch (Exception failure)
            {
                _logger.LogError(failure, "FaultTrace problem enricher {Enricher} failed for fault {ErrorId}", enricher.GetType().FullName, fault.ErrorId);
            }
        }

        return extensions;
    }
}
