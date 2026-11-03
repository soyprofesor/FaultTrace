using System;
using System.Collections.Generic;
using System.Linq;
using FaultTrace.AspNetCore.Configuration;
using FaultTrace.Core.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FaultTrace.AspNetCore.Culture;

// Finds a culture only for message lookup: this never changes the thread culture or constructs a CultureInfo from input.
internal sealed class FaultCultureResolver
{
    private const int MaxValueLength = 35;
    private const int MaxAcceptLanguageEntries = 16;

    private static readonly List<string> DefaultQueryNames = new List<string> { "culture", "lang" };
    private static readonly List<string> DefaultHeaderNames = new List<string> { "X-Culture", "X-Language" };
    private static readonly List<string> DefaultRouteValueNames = new List<string> { "culture" };
    private static readonly List<string> DefaultCookieNames = new List<string> { "culture" };
    private static readonly List<string> DefaultClaimNames = new List<string> { "locale" };

    private readonly IFaultMessageProvider _messages;
    private readonly IOptionsMonitor<FaultTraceAspNetCoreOptions> _options;
    private readonly ILogger _logger;

    public FaultCultureResolver(
        IFaultMessageProvider messages,
        IOptionsMonitor<FaultTraceAspNetCoreOptions> options,
        ILoggerFactory loggerFactory)
    {
        _messages = messages;
        _options = options;
        _logger = loggerFactory.CreateLogger("FaultTrace");
    }

    /// <summary>
    /// Finds the culture of a request. Returns <see langword="null"/> for the default culture, and never throws.
    /// </summary>
    public string? Resolve(HttpContext context)
    {
        try
        {
            var options = _options.CurrentValue.Culture;

            if (options.Mode != FaultTraceCultureMode.Sources)
            {
                var culture = FromRequestLocalization(context);
                if (culture != null || options.Mode == FaultTraceCultureMode.RequestLocalization)
                {
                    return culture;
                }
            }

            return FromSources(context, options);
        }
        catch (Exception failure)
        {
            _logger.LogError(failure, "FaultTrace failed to resolve the response culture");
            return null;
        }
    }

    // This middleware sits outside request localization, so the thread culture is already restored: read the feature.
    private string? FromRequestLocalization(HttpContext context)
    {
        var feature = context.Features.Get<IRequestCultureFeature>();
        return feature == null ? null : Find(feature.RequestCulture.UICulture.Name);
    }

    private string? FromSources(HttpContext context, FaultTraceCultureOptions options)
    {
        return FirstSupported(options.QueryNames ?? DefaultQueryNames, name => context.Request.Query[name].FirstOrDefault())
            ?? FirstSupported(options.HeaderNames ?? DefaultHeaderNames, name => context.Request.Headers[name].FirstOrDefault())
            ?? FirstSupported(options.RouteValueNames ?? DefaultRouteValueNames, name => context.Request.RouteValues[name] as string)
            ?? FirstSupported(options.CookieNames ?? DefaultCookieNames, name => context.Request.Cookies[name])
            ?? FirstSupported(options.ClaimNames ?? DefaultClaimNames, name => context.User?.FindFirst(name)?.Value)
            ?? (options.UseAcceptLanguage ? FromAcceptLanguage(context) : null)
            ?? FromResolver(context, options.Resolver);
    }

    private string? FirstSupported(List<string> names, Func<string, string?> read)
    {
        foreach (var name in names)
        {
            var culture = Find(read(name));
            if (culture != null)
            {
                return culture;
            }
        }

        return null;
    }

    private string? FromAcceptLanguage(HttpContext context)
    {
        var languages = context.Request.GetTypedHeaders().AcceptLanguage;
        if (languages == null)
        {
            return null;
        }

        // OrderByDescending is stable, so equal weights keep the client's order. A weight of zero means "not this one".
        return languages
            .Take(MaxAcceptLanguageEntries)
            .Where(language => (language.Quality ?? 1) > 0)
            .OrderByDescending(language => language.Quality ?? 1)
            .Select(language => Find(language.Value.ToString()))
            .FirstOrDefault(culture => culture != null);
    }

    private string? FromResolver(HttpContext context, Func<HttpContext, string?>? resolver)
    {
        if (resolver == null)
        {
            return null;
        }

        try
        {
            return Find(resolver(context));
        }
        catch (Exception failure)
        {
            _logger.LogError(failure, "FaultTrace culture resolver failed");
            return null;
        }
    }

    private string? Find(string? value)
    {
        return value == null || value.Length > MaxValueLength ? null : _messages.FindCulture(value);
    }
}
