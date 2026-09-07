# MagicCSharp.Scheduling

Background jobs that run on a schedule and stay on it, coordinated across instances.

Separate from the core package because it pulls in `DistributedLock` and the hosting abstractions, which an
application that only uses use cases and events does not need.

## Drift-free

A job that sleeps for its interval and then runs drifts: each run's duration is added to the next wait, so a
nightly job creeps later every day. These schedules compute the *next* occurrence from a fixed anchor and
sleep until then, so a run taking ten minutes does not move the following one.

```csharp
public class NightlyRollupService(IServiceProvider services)
    : ScheduledBackgroundService(services, new TimeOfDaySchedule(new TimeOnly(2, 0)));

public class SyncService(IServiceProvider services)
    : ScheduledBackgroundService(services, new IntervalSchedule(TimeSpan.FromHours(6)));
```

`TimeOfDaySchedule` takes a timezone, so "2am local" survives a daylight-saving change.

## One instance per occurrence

Every instance wakes on the same boundary. Each tries to take a distributed lock named for the job; the one
that gets it runs, the rest go back to sleep. Register a provider that matches how you deploy —
`FileDistributedSynchronizationProvider` is fine for one machine and wrong for several.

`IScheduleStore` records when a job last ran, so an instance starting mid-cycle knows whether the current
occurrence is already done rather than repeating it.
