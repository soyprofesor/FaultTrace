using System;
using System.Collections.Generic;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;

namespace FaultTrace.Core.Options;

/// <summary>
/// Settings shared by all hosts.
/// </summary>
public sealed class FaultTraceOptions
{
    /// <summary>The culture used when none is requested or the requested one isn't supported. The default is English.</summary>
    public string DefaultCulture { get; set; } = CultureNormalizer.English;

    internal Dictionary<string, Dictionary<string, FaultMessage>> Messages { get; } =
        new Dictionary<string, Dictionary<string, FaultMessage>>(StringComparer.OrdinalIgnoreCase);

    internal Dictionary<string, string> Aliases { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Overrides a built-in text or adds one for a new culture.
    /// </summary>
    /// <param name="culture">The culture, for example <c>uz-Latn</c> or a new one like <c>kk</c>.</param>
    /// <param name="code">The fault code. See <see cref="FaultCodes"/>.</param>
    /// <param name="title">The short title.</param>
    /// <param name="detail">The generic text.</param>
    /// <param name="fieldDetail">The text used when a friendly field name is known, with <c>{0}</c> in place of the name.</param>
    /// <returns>These options, for chaining.</returns>
    public FaultTraceOptions SetMessage(string culture, string code, string title, string detail, string? fieldDetail = null)
    {
        var key = CultureNormalizer.Normalize(culture) ?? Guard.NotEmpty(culture, nameof(culture));
        if (!Messages.TryGetValue(key, out var messages))
        {
            messages = new Dictionary<string, FaultMessage>();
            Messages[key] = messages;
        }

        messages[Guard.NotEmpty(code, nameof(code))] = new FaultMessage(title, detail, fieldDetail);
        return this;
    }

    /// <summary>
    /// Adds a non-standard culture value that a project uses, for example <c>oz</c> for <c>uz-Latn</c>.
    /// </summary>
    /// <param name="alias">The value clients send.</param>
    /// <param name="culture">The culture it means.</param>
    /// <returns>These options, for chaining.</returns>
    public FaultTraceOptions AddCultureAlias(string alias, string culture)
    {
        Aliases[Guard.NotEmpty(alias, nameof(alias))] = Guard.NotEmpty(culture, nameof(culture));
        return this;
    }
}
