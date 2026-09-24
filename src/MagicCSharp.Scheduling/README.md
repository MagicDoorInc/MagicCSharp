# MagicCSharp.Scheduling

Background jobs that run on a schedule and stay on it, coordinated across instances. A job is a hosted
service that wakes on a boundary, takes a lock named for itself, and calls a use case — the schedule is not
a place to hide business logic.

Add it when something has to happen every hour, or at 2am local, and you want the test for it to move a
clock rather than wait. Separate from the core package because it pulls in `DistributedLock` and the hosting
abstractions, which an application that only uses use cases and events does not need.

```bash
dotnet add package MagicCSharp.Scheduling
```

## Drift-free

A job that sleeps for its interval and then runs drifts: each run's duration is added to the next wait, so a
nightly job creeps later every day. These schedules compute the *next* occurrence from a fixed anchor and
sleep until then, so a run taking ten minutes does not move the following one.

```csharp
public class NightlyRollupBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IDistributedLockProvider? distributedLockProvider,
    TimeProvider timeProvider,
    ILogger<NightlyRollupBackgroundService> logger)
    : ScheduledBackgroundService(
        serviceScopeFactory, new TimeOfDaySchedule(new TimeOnly(2, 0)), distributedLockProvider, timeProvider, logger)
{
    protected override string ScheduleKey => "nightly-rollup";
    protected override string ServiceName => nameof(NightlyRollupBackgroundService);

    protected override async Task ExecuteScheduledTask(CancellationToken stoppingToken)
    {
        // A scope per run: the service is a singleton and outlives any one of them.
        await using var scope = ServiceScopeFactory.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IRollUpLedgerUseCase>().Execute();
    }
}
```

The job calls a use case. Everything worth testing is in `RollUpLedgerUseCase`, constructed directly with a
`FakeTimeProvider`; the job itself is wiring.

Two schedules:

- `TimeOfDaySchedule(timeOfDay, timeZone)` — daily at a time. The timezone defaults to UTC, and passing one
  means "2am local" survives a daylight-saving change.
- `IntervalSchedule(interval, anchorPoint)` — every so often, on round boundaries counted from the anchor,
  which defaults to the Unix epoch. Six hours gives 00:00, 06:00, 12:00 and 18:00 UTC; one hour gives the top
  of every hour.

Both expose `CalculateNextRun(now)`, so "what is the next run after this instant" is a plain unit test.

Register the job as you would any hosted service, and add `AddMagicScheduling()` — without it there is no
`IScheduleStore`, and the service throws when it first tries to record a run:

```csharp
builder.Services.AddMagicScheduling();
builder.Services.AddHostedService<NightlyRollupBackgroundService>();
```

`MagicCSharp.App` calls `AddMagicScheduling` for you.

## How a run happens

Every instance checks on a round minute — every five by default, at :00, :05, :10 — whether the job's next
run, read from `IScheduleStore`, has passed. When it has, the instance tries a distributed lock named
`scheduled-task:{ScheduleKey}`; the one that gets it re-reads the store, because another instance may
already have run the job in between, then runs it and records the execution and the next occurrence. The
rest go back to sleep.

The next occurrence is recorded even when the run throws, so a failing job is retried at its next scheduled
time rather than every five minutes. The failure is logged with its duration. `CheckIntervalMinutes` and
`LockOutTime` — how long to wait for the lock — are virtual with those defaults.

## One instance per occurrence

The defaults suit one machine: `AddMagicScheduling` registers an in-memory schedule store and a lock backed
by files in a temp directory. Both are registered with try-add, so registering your own first wins.

For more than one instance, both have to be shared. Two processes each holding their own copy of the store
each decide independently that the job is due, and the lock only stops them running at the same instant, not
twice. Implement `IScheduleStore` — four methods — over a table or Redis, and register an
`IDistributedLockProvider` from the `DistributedLock` family that matches how you deploy: Postgres, Redis,
ZooKeeper. `FileDistributedSynchronizationProvider` is fine for one machine and wrong for several.

```csharp
builder.Services.AddSingleton<IScheduleStore, PostgresScheduleStore>();   // yours
builder.Services.AddSingleton<IDistributedLockProvider>(                  // DistributedLock.Postgres
    new PostgresDistributedSynchronizationProvider(connectionString));
builder.Services.AddMagicScheduling();                                     // now a no-op for both
```

The in-memory store also forgets everything on restart, so the first check after a deploy treats every job
as never run and schedules it from now. For a nightly job that means one skipped or one early run around
the deploy.

## In tests

`MagicCSharp.Testing` ships `InMemoryDistributedLockProvider` for real in-process mutual exclusion and
`TrackingDistributedLockProvider` for asserting which names were locked. The job's own logic belongs in the
use case it calls, where `FakeTimeProvider` makes "a month later" one method call.

The service itself waits on the `TimeProvider` it was given, so it can be tested running, too: start it with
a `FakeTimeProvider`, `Advance` the clock past the next occurrence, and it runs — no real waiting.

## Related packages

- [MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp/README.md)
  — the use cases a job should call
- [MagicCSharp.Testing](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing/README.md)
  — `FakeTimeProvider`, the in-memory lock providers

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
