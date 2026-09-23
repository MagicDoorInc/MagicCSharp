# Background Services

This guide owns work that runs on a schedule. The schedule decides **when**; a use case decides **what**.

## The shape

A background service lives in the owning domain's `App/Default/BackgroundServices/`, is named
`{Operation}BackgroundService`, and does one thing: open a scope and call a use case.

```csharp
public class ApplyLateFeesBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IDistributedLockProvider distributedLockProvider,
    TimeProvider timeProvider,
    ILogger<ApplyLateFeesBackgroundService> logger)
    : ScheduledBackgroundService(
        serviceScopeFactory,
        new IntervalSchedule(TimeSpan.FromHours(1)),
        distributedLockProvider,
        timeProvider,
        logger)
{
    protected override string ScheduleKey => "apply-late-fees";
    protected override string ServiceName => nameof(ApplyLateFeesBackgroundService);

    protected override async Task ExecuteScheduledTask(CancellationToken stoppingToken)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var applyLateFees = scope.ServiceProvider.GetRequiredService<IApplyLateFeesUseCase>();

        await applyLateFees.Execute();
    }
}
```

- Open the scope with the base class's `ServiceScopeFactory`, not the constructor parameter — using the
  parameter in the body as well as passing it to the base is compiler error CS9107.
- `IntervalSchedule` for "every hour", `TimeOfDaySchedule(time, timeZone)` for "daily at 02:00 in this zone".
  Schedules are computed from the clock, not from when the last run finished, so they do not drift.
- Every instance wakes on the same boundary and takes a lock named for the service; only one runs each
  occurrence.

## Registering it

The domain registers its own background services, in a module in its `App/Default` project; `Program.cs`
calls the module once:

```csharp
public static class LateFeesAppModule
{
    public static IServiceCollection AddLateFeesApp(this IServiceCollection services)
    {
        services.AddHostedService<ApplyLateFeesBackgroundService>();

        return services;
    }
}
```

```csharp
builder.Services.AddLateFeesApp();   // in {Service}.App/Program.cs
```

## The use case it calls

Everything worth testing is in the use case, which must be:

- **Safe to run again.** A run can repeat, overlap with a manual trigger, or retry after a crash. Check what
  is already done (`ApplyLateFeesUseCase` skips rent that already has a late fee) and take a lock around the
  check-then-write.
- **Batched.** Load the whole eligible set with a few queries, not one per item.
- **Tested through time.** Every scheduled workflow gets a test that arranges eligible state, runs the use
  case, moves the `FakeTimeProvider`, runs it again, and asserts the effect happened once — see
  `testing-conventions.md`.
