using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace FaultTrace.Core.Tests.Translation;

internal sealed class CapturingLogger : ILogger
{
    public List<Entry> Entries { get; } = [];

    public bool ThrowOnLog { get; set; }

    public IDisposable BeginScope<TState>(TState state) => throw new NotSupportedException();

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (ThrowOnLog)
        {
            throw new InvalidOperationException("Logger failure");
        }

        var properties = state as IEnumerable<KeyValuePair<string, object>>;
        Entries.Add(new Entry(
            logLevel,
            exception,
            properties?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? []));
    }

    internal sealed class Entry
    {
        public Entry(LogLevel level, Exception exception, Dictionary<string, object> properties)
        {
            Level = level;
            Exception = exception;
            Properties = properties;
        }

        public LogLevel Level { get; }

        public Exception Exception { get; }

        public Dictionary<string, object> Properties { get; }
    }
}
