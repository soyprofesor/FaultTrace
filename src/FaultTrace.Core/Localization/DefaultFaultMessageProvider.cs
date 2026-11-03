using System;
using System.Collections.Generic;
using FaultTrace.Core.Model;
using FaultTrace.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FaultTrace.Core.Localization;

internal sealed class DefaultFaultMessageProvider : IFaultMessageProvider
{
    private readonly Dictionary<string, Dictionary<string, FaultMessage>> _messages;
    private readonly Dictionary<string, string> _aliases;
    private readonly string _defaultCulture;
    private readonly Func<string, string, FaultMessage?>? _lookup;
    private readonly ILogger _logger;

    public DefaultFaultMessageProvider(IOptions<FaultTraceOptions> options, ILogger? logger = null)
    {
        var values = options.Value;

        _lookup = values.MessageLookup;
        _logger = logger ?? NullLogger.Instance;

        _messages = new Dictionary<string, Dictionary<string, FaultMessage>>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in FaultMessageCatalog.Create())
        {
            _messages[culture.Key] = culture.Value;
        }

        foreach (var culture in values.Messages)
        {
            if (!_messages.TryGetValue(culture.Key, out var target))
            {
                target = new Dictionary<string, FaultMessage>();
                _messages[culture.Key] = target;
            }

            foreach (var message in culture.Value)
            {
                target[message.Key] = message.Value;
            }
        }

        _aliases = new Dictionary<string, string>(values.Aliases, StringComparer.OrdinalIgnoreCase);
        _defaultCulture = FindCulture(values.DefaultCulture) ?? CultureNormalizer.English;
    }

    public string ResolveCulture(string? culture) => FindCulture(culture) ?? _defaultCulture;

    public FaultMessage Get(string code, string culture)
    {
        return Find(code, culture)
            ?? Find(FaultCodes.Unexpected, culture)
            ?? _messages[CultureNormalizer.English][FaultCodes.Unexpected];
    }

    public FaultMessage? Find(string code, string culture)
    {
        return FindIn(culture, code) ?? FindIn(_defaultCulture, code) ?? FindIn(CultureNormalizer.English, code);
    }

    // The project's store comes first in each culture, so a text missing there stays in the requested language.
    private FaultMessage? FindIn(string culture, string code)
    {
        return FromLookup(culture, code)
            ?? (_messages.TryGetValue(culture, out var messages) && messages.TryGetValue(code, out var message) ? message : null);
    }

    private FaultMessage? FromLookup(string culture, string code)
    {
        if (_lookup == null)
        {
            return null;
        }

        try
        {
            return _lookup(code, culture);
        }
        catch (Exception failure)
        {
            _logger.LogError(failure, "FaultTrace message lookup failed for {Code} in {Culture}", code, culture);
            return null;
        }
    }

    public string? FindCulture(string? culture)
    {
        if (culture == null)
        {
            return null;
        }

        var key = culture.Replace('_', '-');
        if (_aliases.TryGetValue(key, out var target))
        {
            key = target;
        }

        var builtIn = CultureNormalizer.Normalize(key);
        if (builtIn != null)
        {
            return builtIn;
        }

        // The name as registered, never the client's spelling: a project's lookup may compare it.
        foreach (var name in _messages.Keys)
        {
            if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }
}
