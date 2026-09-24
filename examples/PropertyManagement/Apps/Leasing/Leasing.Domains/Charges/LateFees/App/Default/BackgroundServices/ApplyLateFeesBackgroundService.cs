using Acme.Leasing.Domains.Charges.LateFees.UseCases;
using MagicCSharp.Scheduling;
using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acme.Leasing.Domains.Charges.LateFees.App.BackgroundServices;

/// <summary>
///     Hourly rather than nightly: rent becomes late at midnight where each property is, and those midnights
///     are spread across the day. The schedule only decides when; the use case decides what.
/// </summary>
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
