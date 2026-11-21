using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Tests;

/// <summary>Records what every logger it creates receives, so a test can assert on the lines written.</summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<RecordedLogEntry> entries = new ConcurrentQueue<RecordedLogEntry>();

    public IReadOnlyList<RecordedLogEntry> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName)
    {
        return new RecordingLogger(categoryName, entries);
    }

    public void Dispose()
    {
    }
}
