using System;
using System.Security.Claims;
using FaultTrace.AspNetCore.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FaultTrace.AspNetCore.Users;

internal sealed class FaultUserResolver
{
    private const string Anonymous = "anonymous";

    private readonly IOptionsMonitor<FaultTraceAspNetCoreOptions> _options;
    private readonly ILogger _logger;

    public FaultUserResolver(IOptionsMonitor<FaultTraceAspNetCoreOptions> options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _logger = loggerFactory.CreateLogger("FaultTrace");
    }

    public string Resolve(HttpContext context)
    {
        var options = _options.CurrentValue;
        var user = options.UserIdResolver != null
            ? ResolveWithDelegate(options.UserIdResolver, context)
            : ReadClaim(context, options.UserIdClaim);

        return string.IsNullOrEmpty(user) ? Anonymous : user!;
    }

    private string? ResolveWithDelegate(Func<HttpContext, string?> resolver, HttpContext context)
    {
        try
        {
            return resolver(context);
        }
        catch (Exception failure)
        {
            _logger.LogError(failure, "FaultTrace user ID resolver failed");
            return null;
        }
    }

    private static string? ReadClaim(HttpContext context, string claim)
    {
        var user = context.User;
        if (user == null)
        {
            return null;
        }

        return user.FindFirst(claim)?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
