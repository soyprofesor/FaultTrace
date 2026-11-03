using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace FaultTrace.AspNetCore.Configuration;

/// <summary>
/// How the response culture is found. The sources are tried in the order of these properties, and a value that
/// isn't a supported culture moves on to the next one. An empty list turns a source off.
/// </summary>
public sealed class FaultTraceCultureOptions
{
    /// <summary>How the culture is found. The default is <see cref="FaultTraceCultureMode.Auto"/>.</summary>
    public FaultTraceCultureMode Mode { get; set; } = FaultTraceCultureMode.Auto;

    /// <summary>Query string names. <see langword="null"/> means <c>culture</c> and <c>lang</c>.</summary>
    public List<string>? QueryNames { get; set; }

    /// <summary>Header names. <see langword="null"/> means <c>X-Culture</c> and <c>X-Language</c>.</summary>
    public List<string>? HeaderNames { get; set; }

    /// <summary>Route value names. <see langword="null"/> means <c>culture</c>.</summary>
    public List<string>? RouteValueNames { get; set; }

    /// <summary>Cookie names. <see langword="null"/> means <c>culture</c>.</summary>
    public List<string>? CookieNames { get; set; }

    /// <summary>Claim names. <see langword="null"/> means <c>locale</c>.</summary>
    public List<string>? ClaimNames { get; set; }

    /// <summary>Whether to read the <c>Accept-Language</c> header, respecting <c>q</c> weights. The default is <see langword="true"/>.</summary>
    public bool UseAcceptLanguage { get; set; } = true;

    /// <summary>A custom source, tried last. A failure is logged and counts as no value.</summary>
    public Func<HttpContext, string?>? Resolver { get; set; }
}
