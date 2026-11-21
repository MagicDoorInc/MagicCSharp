using Microsoft.Extensions.Logging;

namespace PackageSmoke;

/// <summary>Keeps every message written through it, so the smoke check can read the <c>Executing:</c> line.</summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    public List<string> Messages { get; } = [];

    public ILogger CreateLogger(string categoryName)
    {
        return new RecordingLogger(Messages);
    }

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(List<string> messages) : ILogger
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
            messages.Add(formatter(state, exception));
        }
    }
}
