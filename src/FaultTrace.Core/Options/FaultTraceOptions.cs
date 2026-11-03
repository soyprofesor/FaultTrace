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

    internal Dictionary<Type, Func<Exception, FaultProblem?>> ExceptionMappings { get; } = [];

    internal Func<string, string, FaultMessage?>? MessageLookup { get; private set; }

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
            messages = [];
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

    /// <summary>
    /// Gets library-generated texts from the project's own store, such as resource files or a database.
    /// The culture is a parameter, so this never depends on the thread culture.
    /// </summary>
    /// <param name="lookup">
    /// Gets the texts for a fault code and a supported culture. It runs on the error path, so keep it fast.
    /// <see langword="null"/> means the built-in text is used, and a failure is logged and does the same.
    /// </param>
    /// <returns>These options, for chaining.</returns>
    public FaultTraceOptions UseMessages(Func<string, string, FaultMessage?> lookup)
    {
        MessageLookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        return this;
    }

    /// <summary>
    /// Adds a culture that clients may request. Only needed for a culture that has its texts in <see cref="UseMessages"/>.
    /// </summary>
    /// <param name="culture">The culture, for example <c>kk</c>.</param>
    /// <returns>These options, for chaining.</returns>
    public FaultTraceOptions AddCulture(string culture)
    {
        var key = CultureNormalizer.Normalize(culture) ?? Guard.NotEmpty(culture, nameof(culture));
        if (!Messages.ContainsKey(key))
        {
            Messages[key] = [];
        }

        return this;
    }

    /// <summary>
    /// Says what a project's exception means, without changing its class. It also covers derived types, and the most
    /// specific mapping wins. A mapping comes before <see cref="Abstractions.IFaultException"/> and all providers.
    /// </summary>
    /// <typeparam name="TException">The exception type.</typeparam>
    /// <param name="map">Reads the exception. <see langword="null"/> leaves it to the next rule, and a failure is logged and does the same.</param>
    /// <returns>These options, for chaining.</returns>
    public FaultTraceOptions MapException<TException>(Func<TException, FaultProblem?> map)
        where TException : Exception
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        ExceptionMappings[typeof(TException)] = exception => map((TException)exception);
        return this;
    }
}
