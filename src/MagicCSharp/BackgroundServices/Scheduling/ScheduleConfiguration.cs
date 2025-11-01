namespace MagicCSharp.BackgroundServices.Scheduling;

/// <summary>
///     Base class for schedule configurations that determine when a background service should run.
/// </summary>
public abstract class ScheduleConfiguration
{
    /// <summary>
    ///     Calculate the next run time based on the current time and optional last run information.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="lastRunAt">When the task last ran (null if never run).</param>
    /// <param name="lastRunDuration">How long the last run took (null if never run).</param>
    /// <returns>The next time the task should run.</returns>
    public abstract DateTimeOffset CalculateNextRun(
        DateTimeOffset now,
        DateTimeOffset? lastRunAt = null,
        TimeSpan? lastRunDuration = null);
}

/// <summary>
///     Schedule that runs at a specific time of day (e.g., daily at 12:00 PM UTC).
///     Uses drift-free calculation: always calculates from the ideal time, not last run time.
/// </summary>
/// <param name="timeOfDay">The time of day to run at (e.g., 12:00 PM).</param>
/// <param name="timeZone">The timezone to use (defaults to UTC).</param>
public class TimeOfDaySchedule(
    TimeOnly timeOfDay,
    TimeZoneInfo? timeZone = null) : ScheduleConfiguration
{
    /// <summary>
    ///     The time of day to run at (e.g., 12:00 PM).
    /// </summary>
    public TimeOnly TimeOfDay { get; } = timeOfDay;

    /// <summary>
    ///     The timezone to use for the time of day.
    /// </summary>
    public TimeZoneInfo TimeZone { get; } = timeZone ?? TimeZoneInfo.Utc;

    public override DateTimeOffset CalculateNextRun(
        DateTimeOffset now,
        DateTimeOffset? lastRunAt = null,
        TimeSpan? lastRunDuration = null)
    {
        // Convert current time to the target timezone
        var nowInZone = TimeZoneInfo.ConvertTime(now, TimeZone);
        var todayAtTime = new DateTimeOffset(nowInZone.Year, nowInZone.Month, nowInZone.Day, TimeOfDay.Hour,
            TimeOfDay.Minute, TimeOfDay.Second, TimeZone.GetUtcOffset(nowInZone.DateTime));

        // If we've already passed today's time, schedule for tomorrow
        if (now >= todayAtTime)
        {
            var tomorrowAtTime = todayAtTime.AddDays(1);
            return tomorrowAtTime;
        }

        return todayAtTime;
    }
}

/// <summary>
///     Schedule that runs at regular intervals (e.g., every 6 hours).
///     Uses drift-free calculation: calculates from an anchor point, not from last run time.
///     Ensures the next run is always at a round interval that's in the future.
/// </summary>
/// <param name="interval">The interval between runs.</param>
/// <param name="anchorPoint">
///     The anchor point from which to calculate intervals (defaults to Unix epoch).
///     For example, if interval is 6 hours and anchor is midnight, runs will be at 00:00, 06:00, 12:00, 18:00.
/// </param>
public class IntervalSchedule(
    TimeSpan interval,
    DateTimeOffset? anchorPoint = null) : ScheduleConfiguration
{
    /// <summary>
    ///     The interval between runs.
    /// </summary>
    public TimeSpan Interval { get; } = interval <= TimeSpan.Zero
        ? throw new ArgumentException("Interval must be positive", nameof(interval))
        : interval;

    /// <summary>
    ///     The anchor point from which to calculate intervals.
    /// </summary>
    public DateTimeOffset AnchorPoint { get; } = anchorPoint ?? DateTimeOffset.UnixEpoch;

    public override DateTimeOffset CalculateNextRun(
        DateTimeOffset now,
        DateTimeOffset? lastRunAt = null,
        TimeSpan? lastRunDuration = null)
    {
        // Calculate how many intervals have passed since the anchor point
        var timeSinceAnchor = now - AnchorPoint;
        var intervalsPassed = (long)Math.Floor(timeSinceAnchor / Interval);

        // Calculate the next interval boundary after now
        var nextRun = AnchorPoint + Interval * (intervalsPassed + 1);

        // Ensure we're actually in the future (handle edge case where we're exactly on an interval)
        while (nextRun <= now)
        {
            nextRun += Interval;
        }

        return nextRun;
    }
}