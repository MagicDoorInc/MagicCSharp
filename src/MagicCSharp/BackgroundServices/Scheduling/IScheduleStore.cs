namespace MagicCSharp.BackgroundServices.Scheduling;

/// <summary>
///     Interface for storing and retrieving background service schedule information.
///     Implement this to persist schedule data (e.g., in a database, Redis, or in-memory store).
/// </summary>
public interface IScheduleStore
{
    /// <summary>
    ///     Get the next scheduled run time for a background service.
    /// </summary>
    /// <param name="scheduleKey">Unique identifier for the scheduled task.</param>
    /// <returns>The next run time, or null if not yet scheduled.</returns>
    Task<DateTimeOffset?> GetNextRunTime(string scheduleKey);

    /// <summary>
    ///     Set the next scheduled run time for a background service.
    /// </summary>
    /// <param name="scheduleKey">Unique identifier for the scheduled task.</param>
    /// <param name="nextRun">When the task should run next.</param>
    Task SetNextRunTime(string scheduleKey, DateTimeOffset nextRun);

    /// <summary>
    ///     Record that a task has completed execution.
    /// </summary>
    /// <param name="scheduleKey">Unique identifier for the scheduled task.</param>
    /// <param name="executionStart">When execution started.</param>
    /// <param name="executionDuration">How long execution took.</param>
    /// <param name="nextRun">When the task should run next.</param>
    Task RecordExecution(
        string scheduleKey,
        DateTimeOffset executionStart,
        TimeSpan executionDuration,
        DateTimeOffset nextRun);

    /// <summary>
    ///     Get the last execution information for a scheduled task.
    /// </summary>
    /// <param name="scheduleKey">Unique identifier for the scheduled task.</param>
    /// <returns>Last run time and duration, or null if never run.</returns>
    Task<(DateTimeOffset? lastRunAt, TimeSpan? duration)?> GetLastExecution(string scheduleKey);
}