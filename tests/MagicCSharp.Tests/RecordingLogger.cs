using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Tests;

/// <summary>A logger for one category that records every line, at every level, into its provider's list.</summary>
public sealed class RecordingLogger(string category, ConcurrentQueue<RecordedLogEntry> entries) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        entries.Enqueue(new RecordedLogEntry
        {
            Category = category,
            Level = logLevel,
            Message = formatter(state, exception),
        });
    }
}
