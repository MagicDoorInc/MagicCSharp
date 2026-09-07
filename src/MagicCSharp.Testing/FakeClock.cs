using MagicCSharp.Infrastructure;

namespace MagicCSharp.Testing;

/// <summary>
///     An <see cref="IClock" /> the test moves by hand.
///     <para>
///         This is the whole reason <see cref="IClock" /> exists rather than calling
///         <see cref="DateTimeOffset.UtcNow" /> directly. A late fee that applies after thirty days, a lease that
///         ends at midnight in the property's timezone, a token that expires in an hour — each is testable in
///         milliseconds by advancing this clock, and untestable in any reasonable time without it.
///     </para>
///     <para>
///         Register it in place of the real clock and hold a reference:
///         <c>services.RemoveAll&lt;IClock&gt;(); services.AddSingleton&lt;IClock&gt;(clock);</c>
///     </para>
/// </summary>
public class FakeClock : IClock
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;

    public DateTimeOffset Now()
    {
        return now;
    }

    /// <summary>
    ///     Set the clock to an exact instant.
    /// </summary>
    public void SetTime(DateTimeOffset time)
    {
        now = time;
    }

    /// <summary>
    ///     Set the clock to a UTC date and time.
    /// </summary>
    public void SetTime(
        int year,
        int month,
        int day,
        int hour = 0,
        int minute = 0,
        int second = 0)
    {
        now = new DateTimeOffset(year, month, day, hour,
            minute, second, TimeSpan.Zero);
    }

    /// <summary>
    ///     Move the clock forward by a duration. Negative durations move it back, which is occasionally what a
    ///     test of backdated data needs.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        now = now.Add(duration);
    }

    /// <summary>
    ///     Move the clock forward whole days.
    /// </summary>
    public void AdvanceDays(int days)
    {
        Advance(TimeSpan.FromDays(days));
    }

    /// <summary>
    ///     Move the clock forward whole hours.
    /// </summary>
    public void AdvanceHours(int hours)
    {
        Advance(TimeSpan.FromHours(hours));
    }

    /// <summary>
    ///     Move the clock to the start of the next day, for testing work that runs on a daily boundary.
    /// </summary>
    public void AdvanceToNextMidnight()
    {
        now = new DateTimeOffset(now.Date.AddDays(1), TimeSpan.Zero);
    }

    /// <summary>
    ///     Move the clock to the next occurrence of a time of day, today if it has not passed yet and tomorrow
    ///     otherwise.
    /// </summary>
    public void AdvanceTo(int hour, int minute = 0, int second = 0)
    {
        var target = new DateTimeOffset(now.Year, now.Month, now.Day, hour,
            minute, second, now.Offset);

        if (target <= now)
        {
            target = target.AddDays(1);
        }

        now = target;
    }
}
