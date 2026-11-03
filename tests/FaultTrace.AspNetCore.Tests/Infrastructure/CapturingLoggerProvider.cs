using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace FaultTrace.AspNetCore.Tests.Infrastructure;

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new ConcurrentQueue<LogEntry>();

    public IReadOnlyList<LogEntry> FaultTraceEntries => _entries.Where(entry => entry.Category == "FaultTrace").ToArray();

    public IReadOnlyList<LogEntry> Faults => FaultTraceEntries.Where(entry => entry.Properties.ContainsKey("Code")).ToArray();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class Logger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<LogEntry> _entries;

        public Logger(string category, ConcurrentQueue<LogEntry> entries)
        {
            _category = category;
            _entries = entries;
        }

#if NET8_0_OR_GREATER
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => new Scope();
#else
        public IDisposable BeginScope<TState>(TState state) => new Scope();
#endif

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state as IEnumerable<KeyValuePair<string, object?>>;
            _entries.Enqueue(new LogEntry(
                _category,
                logLevel,
                exception,
                formatter(state, exception),
                properties?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? new Dictionary<string, object?>()));
        }

        private sealed class Scope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}

internal sealed class LogEntry
{
    public LogEntry(string category, LogLevel level, Exception? exception, string text, Dictionary<string, object?> properties)
    {
        Category = category;
        Level = level;
        Exception = exception;
        Text = text;
        Properties = properties;
    }

    public string Category { get; }

    public LogLevel Level { get; }

    public Exception? Exception { get; }

    public string Text { get; }

    public Dictionary<string, object?> Properties { get; }
}
