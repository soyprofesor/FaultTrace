using System;
using FaultTrace.Core.Model;
using FaultTrace.Core.Options;
using Microsoft.AspNetCore.Http;

namespace FaultTrace.AspNetCore.Configuration;

/// <summary>
/// Settings for error responses. Everything except the culture texts can be reloaded without a restart.
/// </summary>
public sealed class FaultTraceAspNetCoreOptions
{
    /// <summary>The default for <see cref="TypeBaseUri"/>.</summary>
    public const string DefaultTypeBaseUri = "https://soyprofesor.github.io/FaultTrace/errors/";

    private readonly FaultTraceOptions _core;

    /// <summary>
    /// Creates the settings with their defaults.
    /// </summary>
    public FaultTraceAspNetCoreOptions()
        : this(new FaultTraceOptions())
    {
    }

    internal FaultTraceAspNetCoreOptions(FaultTraceOptions core)
    {
        _core = core;
    }

    /// <summary>When a response may contain the <c>debug</c> block. The default is <see cref="Configuration.DetailExposure.DevelopmentOnly"/>.</summary>
    public DetailExposure DetailExposure { get; set; } = DetailExposure.DevelopmentOnly;

    /// <summary>
    /// Narrows <see cref="DetailExposure"/>, for example to users in a <c>Developer</c> role. It can't widen it,
    /// and a failure is logged and counts as <see langword="false"/>.
    /// </summary>
    public Func<HttpContext, bool>? DetailExposurePredicate { get; set; }

    /// <summary>The start of the <c>type</c> URI. The fault code follows it, with <c>_</c> replaced by <c>-</c>.</summary>
    public string TypeBaseUri { get; set; } = DefaultTypeBaseUri;

    /// <summary>The <c>Retry-After</c> value on a 503 response, in seconds. The default is 5, and zero or less sends no header.</summary>
    public int RetryAfterSeconds { get; set; } = 5;

    /// <summary>The claim that holds the user ID. The default is <c>sub</c>; the mapped <c>NameIdentifier</c> claim is also checked.</summary>
    public string UserIdClaim { get; set; } = "sub";

    /// <summary>Gets the user ID instead of reading a claim. A <see langword="null"/> result or a failure means <c>anonymous</c>.</summary>
    public Func<HttpContext, string?>? UserIdResolver { get; set; }

    /// <summary>How the response culture is found.</summary>
    public FaultTraceCultureOptions Culture { get; } = new FaultTraceCultureOptions();

    /// <summary>The culture used when none is requested or the requested one isn't supported. The default is English. Read once at startup.</summary>
    public string DefaultCulture
    {
        get => _core.DefaultCulture;
        set => _core.DefaultCulture = value;
    }

    /// <summary>
    /// Overrides a built-in text or adds one for a new culture. Read once at startup.
    /// </summary>
    /// <param name="culture">The culture, for example <c>uz-Latn</c> or a new one like <c>kk</c>.</param>
    /// <param name="code">The fault code. See <see cref="FaultCodes"/>.</param>
    /// <param name="title">The short title.</param>
    /// <param name="detail">The generic text.</param>
    /// <param name="fieldDetail">The text used when a friendly field name is known, with <c>{0}</c> in place of the name.</param>
    /// <returns>These options, for chaining.</returns>
    public FaultTraceAspNetCoreOptions SetMessage(string culture, string code, string title, string detail, string? fieldDetail = null)
    {
        _core.SetMessage(culture, code, title, detail, fieldDetail);
        return this;
    }

    /// <summary>
    /// Adds a non-standard culture value that a project uses, for example <c>oz</c> for <c>uz-Latn</c>. Read once at startup.
    /// </summary>
    /// <param name="alias">The value clients send.</param>
    /// <param name="culture">The culture it means.</param>
    /// <returns>These options, for chaining.</returns>
    public FaultTraceAspNetCoreOptions AddCultureAlias(string alias, string culture)
    {
        _core.AddCultureAlias(alias, culture);
        return this;
    }
}
