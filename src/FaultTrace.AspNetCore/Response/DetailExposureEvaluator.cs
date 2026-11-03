using System;
using FaultTrace.AspNetCore.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FaultTrace.AspNetCore.Response;

internal sealed class DetailExposureEvaluator
{
    private readonly IOptionsMonitor<FaultTraceAspNetCoreOptions> _options;
    private readonly bool _isDevelopment;
    private readonly ILogger _logger;

    public DetailExposureEvaluator(
        IOptionsMonitor<FaultTraceAspNetCoreOptions> options,
        IServiceProvider services,
        ILoggerFactory loggerFactory)
    {
        _options = options;
        _logger = loggerFactory.CreateLogger("FaultTrace");

        // An unknown environment counts as production, the safe side.
        _isDevelopment = services.GetService<IWebHostEnvironment>()?.IsDevelopment() ?? false;
    }

    public bool IsAllowed(HttpContext context)
    {
        var options = _options.CurrentValue;

        switch (options.DetailExposure)
        {
            case DetailExposure.Always:
                break;
            case DetailExposure.DevelopmentOnly when _isDevelopment:
                break;
            default:
                return false;
        }

        if (options.DetailExposurePredicate == null)
        {
            return true;
        }

        try
        {
            return options.DetailExposurePredicate(context);
        }
        catch (Exception failure)
        {
            _logger.LogError(failure, "FaultTrace detail exposure predicate failed");
            return false;
        }
    }
}
