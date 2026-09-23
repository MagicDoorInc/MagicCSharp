using System.Collections.Concurrent;

namespace MagicCSharp.Scheduling;

/// <summary>
///     An <see cref="IScheduleStore" /> held in process memory.
///     <para>
///         <b>Single instance only.</b> Two processes each keep their own copy, so each decides
///         independently that a job is due and both run it. The distributed lock stops them running at the
///         same instant; it does not stop the job running twice. For more than one instance, back the store
///         with something they share — a table, Redis, ZooKeeper.
///     </para>
///     <para>
///         Schedule state is also lost on restart, so the first tick after a deploy treats every job as
///         never run and schedules it from now. For a nightly job that means one skipped or one early run
///         around the deploy.
///     </para>
///     <para>
///         Registered by <c>AddInMemoryScheduleStore()</c>. It is the right default for development, for a
///         single-instance service, and for a job that is safe to run twice.
///     </para>
/// </summary>
public class InMemoryScheduleStore : IScheduleStore
{
    private readonly ConcurrentDictionary<string, Execution> executions = new ConcurrentDictionary<string, Execution>(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> nextRuns = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.Ordinal);

    public Task<DateTimeOffset?> GetNextRunTime(string scheduleKey)
    {
        return Task.FromResult(nextRuns.TryGetValue(scheduleKey, out var next) ? next : (DateTimeOffset?)null);
    }

    public Task SetNextRunTime(string scheduleKey, DateTimeOffset nextRun)
    {
        nextRuns[scheduleKey] = nextRun;
        return Task.CompletedTask;
    }

    public Task RecordExecution(
        string scheduleKey,
        DateTimeOffset executionStart,
        TimeSpan executionDuration,
        DateTimeOffset nextRun)
    {
        executions[scheduleKey] = new Execution(executionStart, executionDuration);
        nextRuns[scheduleKey] = nextRun;
        return Task.CompletedTask;
    }

    public Task<(DateTimeOffset? lastRunAt, TimeSpan? duration)?> GetLastExecution(string scheduleKey)
    {
        if (!executions.TryGetValue(scheduleKey, out var execution))
        {
            return Task.FromResult<(DateTimeOffset?, TimeSpan?)?>(null);
        }

        return Task.FromResult<(DateTimeOffset?, TimeSpan?)?>((execution.StartedAt, execution.Duration));
    }

    private record Execution(DateTimeOffset StartedAt, TimeSpan Duration);
}
