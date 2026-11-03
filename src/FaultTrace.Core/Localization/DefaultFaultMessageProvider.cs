using System;
using System.Collections.Generic;
using FaultTrace.Core.Model;
using FaultTrace.Core.Options;
using Microsoft.Extensions.Options;

namespace FaultTrace.Core.Localization;

internal sealed class DefaultFaultMessageProvider : IFaultMessageProvider
{
    private readonly Dictionary<string, Dictionary<string, FaultMessage>> _messages;
    private readonly Dictionary<string, string> _aliases;
    private readonly string _defaultCulture;

    public DefaultFaultMessageProvider(IOptions<FaultTraceOptions> options)
    {
        var values = options.Value;

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

    private FaultMessage? Find(string code, string culture)
    {
        return FindIn(culture, code) ?? FindIn(_defaultCulture, code) ?? FindIn(CultureNormalizer.English, code);
    }

    private FaultMessage? FindIn(string culture, string code)
    {
        return _messages.TryGetValue(culture, out var messages) && messages.TryGetValue(code, out var message)
            ? message
            : null;
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

        return _messages.ContainsKey(key) ? key : null;
    }
}
