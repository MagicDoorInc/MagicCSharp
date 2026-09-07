using MagicCSharp.Testing;
using Xunit;

namespace MagicCSharp.Tests;

public class FakeClockTests
{
    private readonly FakeClock clock = new FakeClock();

    [Fact]
    public void SetTime_components_are_interpreted_as_UTC()
    {
        clock.SetTime(2026, 3, 1, 14,
            30, 15);

        Assert.Equal(new DateTimeOffset(2026, 3, 1, 14,
            30, 15, TimeSpan.Zero), clock.Now());
    }

    [Fact]
    public void Advance_moves_forward_by_the_duration()
    {
        clock.SetTime(2026, 3, 1);

        clock.Advance(TimeSpan.FromHours(6));

        Assert.Equal(new DateTimeOffset(2026, 3, 1, 6,
            0, 0, TimeSpan.Zero), clock.Now());
    }

    [Fact]
    public void AdvanceDays_crosses_a_month_boundary()
    {
        clock.SetTime(2026, 1, 30);

        clock.AdvanceDays(3);

        Assert.Equal(new DateTimeOffset(2026, 2, 2, 0,
            0, 0, TimeSpan.Zero), clock.Now());
    }

    [Fact]
    public void AdvanceToNextMidnight_lands_on_the_start_of_the_next_day()
    {
        clock.SetTime(2026, 3, 1, 23,
            45, 0);

        clock.AdvanceToNextMidnight();

        Assert.Equal(new DateTimeOffset(2026, 3, 2, 0,
            0, 0, TimeSpan.Zero), clock.Now());
    }

    [Fact]
    public void AdvanceTo_moves_to_a_time_later_today()
    {
        clock.SetTime(2026, 3, 1, 9,
            0, 0);

        clock.AdvanceTo(17);

        Assert.Equal(new DateTimeOffset(2026, 3, 1, 17,
            0, 0, TimeSpan.Zero), clock.Now());
    }

    [Fact]
    public void AdvanceTo_a_time_already_past_moves_to_tomorrow()
    {
        clock.SetTime(2026, 3, 1, 18,
            0, 0);

        clock.AdvanceTo(9);

        Assert.Equal(new DateTimeOffset(2026, 3, 2, 9,
            0, 0, TimeSpan.Zero), clock.Now());
    }

    [Fact]
    public void AdvanceTo_the_current_time_exactly_moves_a_full_day()
    {
        // Otherwise a daily job scheduled for exactly now would fire twice for the same day.
        clock.SetTime(2026, 3, 1, 9,
            0, 0);

        clock.AdvanceTo(9);

        Assert.Equal(new DateTimeOffset(2026, 3, 2, 9,
            0, 0, TimeSpan.Zero), clock.Now());
    }
}
