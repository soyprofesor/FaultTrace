using System;
using FaultTrace.Core.Localization;
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

    /// <summary>
    /// Whether a 4xx or 5xx response without a body gets the template. The default is <see langword="true"/>.
    /// Set it to <see langword="false"/> to keep such responses as they are. Unhandled exceptions always get the template.
    /// </summary>
    public bool HandleEmptyStatus { get; set; } = true;

    /// <summary>
    /// Narrows <see cref="HandleEmptyStatus"/> to some requests, for example to leave a legacy path alone.
    /// A failure is logged, and the response still gets the template.
    /// </summary>
    public Func<HttpContext, bool>? EmptyStatusFilter { get; set; }

    /// <summary>
    /// Whether ModelState failures and the empty results of <c>[ApiController]</c> get the template, replacing the MVC
    /// format and any <c>InvalidModelStateResponseFactory</c>. The default is <see langword="true"/>. Read once at startup.
    /// </summary>
    public bool HandleValidation { get; set; } = true;

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

    /// <summary>
    /// Gets library-generated texts from the project's own store, such as resource files or a database.
    /// The culture is a parameter, so this never depends on the thread culture. Read once at startup.
    /// </summary>
    /// <param name="lookup">
    /// Gets the texts for a fault code and a supported culture. It runs on the error path, so keep it fast.
    /// <see langword="null"/> means the built-in text is used, and a failure is logged and does the same.
    /// </param>
    /// <returns>These options, for chaining.</returns>
    public FaultTraceAspNetCoreOptions UseMessages(Func<string, string, FaultMessage?> lookup)
    {
        _core.UseMessages(lookup);
        return this;
    }

    /// <summary>
    /// Adds a culture that clients may request. Only needed for a culture that has its texts in <see cref="UseMessages"/>. Read once at startup.
    /// </summary>
    /// <param name="culture">The culture, for example <c>kk</c>.</param>
    /// <returns>These options, for chaining.</returns>
    public FaultTraceAspNetCoreOptions AddCulture(string culture)
    {
        _core.AddCulture(culture);
        return this;
    }

    /// <summary>
    /// Says what a project's exception means, without changing its class. It also covers derived types, and the most
    /// specific mapping wins. A mapping comes before <see cref="Core.Abstractions.IFaultException"/> and all providers. Read once at startup.
    /// </summary>
    /// <typeparam name="TException">The exception type.</typeparam>
    /// <param name="map">Reads the exception. <see langword="null"/> leaves it to the next rule, and a failure is logged and does the same.</param>
    /// <returns>These options, for chaining.</returns>
    public FaultTraceAspNetCoreOptions MapException<TException>(Func<TException, FaultProblem?> map)
        where TException : Exception
    {
        _core.MapException(map);
        return this;
    }
}
