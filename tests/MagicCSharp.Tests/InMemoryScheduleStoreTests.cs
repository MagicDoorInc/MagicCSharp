using MagicCSharp.Scheduling;
using Xunit;

namespace MagicCSharp.Tests;

public class InMemoryScheduleStoreTests
{
    private readonly InMemoryScheduleStore store = new InMemoryScheduleStore();

    [Fact]
    public async Task An_unknown_key_has_no_next_run()
    {
        // ScheduledBackgroundService reads this on its first tick and takes null to mean "never scheduled",
        // so it must be null rather than default(DateTimeOffset) — which would read as the year 1.
        Assert.Null(await store.GetNextRunTime("never-seen"));
    }

    [Fact]
    public async Task An_unknown_key_has_no_last_execution()
    {
        Assert.Null(await store.GetLastExecution("never-seen"));
    }

    [Fact]
    public async Task The_next_run_round_trips()
    {
        var next = new DateTimeOffset(2026, 3, 1, 2,
            0, 0, TimeSpan.Zero);

        await store.SetNextRunTime("nightly", next);

        Assert.Equal(next, await store.GetNextRunTime("nightly"));
    }

    [Fact]
    public async Task Recording_an_execution_stores_the_run_and_the_next_time()
    {
        var started = new DateTimeOffset(2026, 3, 1, 2,
            0, 0, TimeSpan.Zero);
        var next = started.AddDays(1);

        await store.RecordExecution("nightly", started, TimeSpan.FromSeconds(90), next);

        var last = await store.GetLastExecution("nightly");

        Assert.NotNull(last);
        Assert.Equal(started, last!.Value.lastRunAt);
        Assert.Equal(TimeSpan.FromSeconds(90), last.Value.duration);
        Assert.Equal(next, await store.GetNextRunTime("nightly"));
    }

    [Fact]
    public async Task Keys_do_not_collide()
    {
        var morning = new DateTimeOffset(2026, 3, 1, 6,
            0, 0, TimeSpan.Zero);
        var evening = new DateTimeOffset(2026, 3, 1, 18,
            0, 0, TimeSpan.Zero);

        await store.SetNextRunTime("morning", morning);
        await store.SetNextRunTime("evening", evening);

        Assert.Equal(morning, await store.GetNextRunTime("morning"));
        Assert.Equal(evening, await store.GetNextRunTime("evening"));
    }
}
