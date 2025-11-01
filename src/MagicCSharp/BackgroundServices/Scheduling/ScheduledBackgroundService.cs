using MagicCSharp.Infrastructure;
using Medallion.Threading;
using Medallion.Threading.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.BackgroundServices.Scheduling;

/// <summary>
///     Base class for background services that run on a schedule.
///     Provides drift-free scheduling by checking on round intervals (every 5 minutes: :00, :05, :10, :15, etc.).
///     Uses distributed locking to ensure only one instance executes the task.
/// </summary>
public abstract class ScheduledBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    ScheduleConfiguration scheduleConfiguration,
    IDistributedLockProvider? lockProvider,
    IClock? clock,
    ILogger logger) : BackgroundService
{
    private readonly IClock clock = clock ?? new DateTimeClock();

    private readonly IDistributedLockProvider lockProvider = lockProvider ??
                                                             new FileDistributedSynchronizationProvider(
                                                                 new DirectoryInfo(Path.Combine(Path.GetTempPath(),
                                                                     "magiccsharp-locks")));

    /// <summary>
    ///     Unique key for this scheduled task (e.g., "subscription-expiration-check").
    /// </summary>
    protected abstract string ScheduleKey { get; }

    /// <summary>
    ///     Human-readable name for logging.
    /// </summary>
    protected abstract string ServiceName { get; }

    /// <summary>
    ///     How often to check if it's time to run (in minutes).
    ///     Default is 5 minutes, meaning checks happen at :00, :05, :10, :15, etc.
    /// </summary>
    protected virtual int CheckIntervalMinutes => 5;

    /// <summary>
    ///     How long the distributed lock should be held.
    ///     Default is 15 minutes.
    /// </summary>
    protected virtual TimeSpan LockOutTime => TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "{ServiceName} started - checking every {Interval} minutes at round intervals (:00, :{Interval:D2}, etc.)",
            ServiceName, CheckIntervalMinutes, CheckIntervalMinutes);

        // Initialize the schedule if needed
        await InitializeSchedule();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunIfDue(stoppingToken);
            } catch (Exception ex)
            {
                logger.LogError(ex, "{ServiceName} check cycle failed", ServiceName);
            }

            // Calculate delay until next round interval
            var delay = CalculateDelayUntilNextCheckInterval();
            logger.LogDebug("{ServiceName} sleeping for {Delay:F1} seconds until next check at {NextCheck}",
                ServiceName, delay.TotalSeconds, DateTimeOffset.UtcNow.Add(delay));

            await Task.Delay(delay, stoppingToken);
        }

        logger.LogInformation("{ServiceName} stopped", ServiceName);
    }

    /// <summary>
    ///     Calculate how long to wait until the next round interval.
    ///     For example, if CheckIntervalMinutes is 5 and current time is 12:03:45,
    ///     this will return the delay until 12:05:00.
    /// </summary>
    private TimeSpan CalculateDelayUntilNextCheckInterval()
    {
        var now = clock.Now();
        var currentMinute = now.Minute;
        var currentSecond = now.Second;
        var currentMillisecond = now.Millisecond;

        // Calculate next round interval minute
        var nextIntervalMinute = (currentMinute / CheckIntervalMinutes + 1) * CheckIntervalMinutes;

        // If we overflow the hour, adjust
        var minutesToAdd = nextIntervalMinute - currentMinute;
        if (nextIntervalMinute >= 60)
        {
            minutesToAdd = 60 - currentMinute;
        }

        // Calculate exact delay to reach the next round minute at :00 seconds
        var delay = TimeSpan.FromMinutes(minutesToAdd) - TimeSpan.FromSeconds(currentSecond) -
                    TimeSpan.FromMilliseconds(currentMillisecond);

        return delay;
    }

    private async Task InitializeSchedule()
    {
        using var scope = serviceScopeFactory.CreateScope();
        var scheduleStore = scope.ServiceProvider.GetRequiredService<IScheduleStore>();

        var nextRun = await scheduleStore.GetNextRunTime(ScheduleKey);
        if (nextRun == null)
        {
            // First time running - calculate initial next run time
            var now = clock.Now();
            var initialNextRun = scheduleConfiguration.CalculateNextRun(now);
            await scheduleStore.SetNextRunTime(ScheduleKey, initialNextRun);

            logger.LogInformation("{ServiceName} initialized - first run scheduled for {NextRun}", ServiceName,
                initialNextRun);
        }
    }

    private async Task CheckAndRunIfDue(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var scheduleStore = scope.ServiceProvider.GetRequiredService<IScheduleStore>();

        var now = clock.Now();
        var nextRun = await scheduleStore.GetNextRunTime(ScheduleKey);

        if (nextRun == null)
        {
            logger.LogWarning("{ServiceName} has no scheduled next run - reinitializing", ServiceName);
            await InitializeSchedule();
            return;
        }

        // Check if we're past the scheduled run time
        if (now < nextRun.Value)
        {
            // Not time yet
            logger.LogTrace("{ServiceName} not due yet (next run: {NextRun}, now: {Now})", ServiceName, nextRun.Value,
                now);
            return;
        }

        // Try to acquire distributed lock to ensure only one instance runs the task
        var lockKey = $"scheduled-task:{ScheduleKey}";
        logger.LogDebug("{ServiceName} attempting to acquire lock for execution", ServiceName);

        await using var distributedLock = await lockProvider.TryAcquireLockAsync(lockKey, LockOutTime, stoppingToken);
        if (distributedLock == null)
        {
            logger.LogDebug("{ServiceName} could not acquire lock - another instance is likely running the task",
                ServiceName);
            return;
        }

        logger.LogDebug("{ServiceName} acquired lock - checking if still due", ServiceName);

        // Double-check that we're still past the scheduled run time after acquiring the lock
        // Another instance might have already executed and updated the next run time
        var nextRunAfterLock = await scheduleStore.GetNextRunTime(ScheduleKey);
        if (nextRunAfterLock == null || clock.Now() < nextRunAfterLock.Value)
        {
            logger.LogDebug("{ServiceName} not due anymore after acquiring lock (another instance already ran it)",
                ServiceName);
            return;
        }

        logger.LogInformation("{ServiceName} starting scheduled execution (was due at {DueTime}, running at {Now})",
            ServiceName, nextRunAfterLock.Value, clock.Now());

        var executionStart = clock.Now();

        try
        {
            // Get last execution info to pass to CalculateNextRun
            var lastExecution = await scheduleStore.GetLastExecution(ScheduleKey);

            // Execute the scheduled task
            await ExecuteScheduledTask(stoppingToken);

            var executionDuration = clock.Now() - executionStart;

            // Calculate next run time (drift-free - based on ideal time, not actual run time)
            // Pass last execution info so the schedule can use it if needed
            var calculatedNextRun = scheduleConfiguration.CalculateNextRun(
                clock.Now(), lastExecution?.lastRunAt, lastExecution?.duration);

            // Record execution and update next run time
            await scheduleStore.RecordExecution(ScheduleKey, executionStart, executionDuration, calculatedNextRun);

            logger.LogInformation("{ServiceName} completed in {Duration:F1}s - next run scheduled for {NextRun}",
                ServiceName, executionDuration.TotalSeconds, calculatedNextRun);
        } catch (Exception ex)
        {
            var executionDuration = clock.Now() - executionStart;
            logger.LogError(ex, "{ServiceName} failed after {Duration:F1}s", ServiceName,
                executionDuration.TotalSeconds);

            // Still calculate next run even if execution failed
            var lastExecution = await scheduleStore.GetLastExecution(ScheduleKey);
            var calculatedNextRun = scheduleConfiguration.CalculateNextRun(
                clock.Now(), lastExecution?.lastRunAt, lastExecution?.duration);

            await scheduleStore.RecordExecution(ScheduleKey, executionStart, executionDuration, calculatedNextRun);

            logger.LogInformation("{ServiceName} next run scheduled for {NextRun} despite failure", ServiceName,
                calculatedNextRun);
        }
    }

    /// <summary>
    ///     Implement this method to define the task that runs on schedule.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token.</param>
    protected abstract Task ExecuteScheduledTask(CancellationToken stoppingToken);
}