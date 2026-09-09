# MagicCSharp.Scheduling

Background jobs that run on a schedule and stay on it, coordinated across instances.

Separate from the core package because it pulls in `DistributedLock` and the hosting abstractions, which an
application that only uses use cases and events does not need.

## Drift-free

A job that sleeps for its interval and then runs drifts: each run's duration is added to the next wait, so a
nightly job creeps later every day. These schedules compute the *next* occurrence from a fixed anchor and
sleep until then, so a run taking ten minutes does not move the following one.

```csharp
public class NightlyRollupService(
    IServiceScopeFactory scopes,
    IDistributedLockProvider? locks,
    IClock clock,
    ILogger<NightlyRollupService> logger)
    : ScheduledBackgroundService(scopes, new TimeOfDaySchedule(new TimeOnly(2, 0)), locks, clock, logger)
{
    protected override string ScheduleKey => "nightly-rollup";
    protected override string ServiceName => nameof(NightlyRollupService);

    protected override async Task ExecuteScheduledTask(CancellationToken stoppingToken)
    {
        // A scope per run: the service is a singleton and outlives any one of them.
        await using var scope = scopes.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IRollUpLedgerUseCase>().Execute();
    }
}
```

`ScheduleKey` names the distributed lock, so every instance of this service competes for the same one.
`IntervalSchedule` is the other schedule, for "every six hours" rather than "at a time of day".

Register it as you would any hosted service, and add `AddMagicScheduling()` — without it there is no
`IScheduleStore` and the service throws when it first tries to record a run:

```csharp
builder.Services.AddMagicScheduling();
builder.Services.AddHostedService<NightlyRollupService>();
```

`TimeOfDaySchedule` takes a timezone, so "2am local" survives a daylight-saving change.

## One instance per occurrence

Every instance wakes on the same boundary. Each tries to take a distributed lock named for the job; the one
that gets it runs, the rest go back to sleep. Register a provider that matches how you deploy —
`FileDistributedSynchronizationProvider` is fine for one machine and wrong for several.

`IScheduleStore` records when a job last ran, so an instance starting mid-cycle knows whether the current
occurrence is already done rather than repeating it.
