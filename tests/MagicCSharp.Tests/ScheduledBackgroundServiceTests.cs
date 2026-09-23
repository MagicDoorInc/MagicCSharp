using MagicCSharp.Scheduling;
using MagicCSharp.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace MagicCSharp.Tests;

public class ScheduledBackgroundServiceTests
{
    [Fact]
    public async Task A_derived_service_opens_scopes_from_the_factory_it_was_built_with()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ScopedWork>();
        await using var serviceProvider = services.BuildServiceProvider();

        var nightlyWorkBackgroundService = new NightlyWorkBackgroundService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new FakeTimeProvider(),
            NullLogger<NightlyWorkBackgroundService>.Instance);

        // Act
        await nightlyWorkBackgroundService.RunOnce();

        // Assert
        Assert.True(nightlyWorkBackgroundService.HasRun);
    }

    [Fact]
    public async Task A_running_service_fires_when_the_clock_reaches_its_next_run()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ScopedWork>();
        services.AddInMemoryScheduleStore();
        await using var serviceProvider = services.BuildServiceProvider();

        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero));
        var nightlyWorkBackgroundService = new NightlyWorkBackgroundService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            NullLogger<NightlyWorkBackgroundService>.Instance);

        await nightlyWorkBackgroundService.StartAsync(CancellationToken.None);

        // Act — move the fake clock past the next midnight, a few minutes at a time, the way real time would
        // pass. The service waits on this clock, so nothing here waits a real day.
        for (var step = 0; step < 24 * 12 && !nightlyWorkBackgroundService.HasRun; step++)
        {
            timeProvider.Advance(TimeSpan.FromMinutes(5));
            await nightlyWorkBackgroundService.WaitForIdle();
        }

        await nightlyWorkBackgroundService.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(nightlyWorkBackgroundService.HasRun);
    }

    private class ScopedWork
    {
        public bool HasRun { get; private set; }

        public void Run()
        {
            HasRun = true;
        }
    }

    /// <summary>
    ///     Written the way the Scheduling README shows a service: the scope factory goes to the base
    ///     constructor, and the body opens its scope through <c>ServiceScopeFactory</c>. With CS9107 an error in
    ///     this project, the class only compiles while that stays the pattern that works.
    /// </summary>
    private class NightlyWorkBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        TimeProvider timeProvider,
        ILogger<NightlyWorkBackgroundService> logger)
        : ScheduledBackgroundService(
            serviceScopeFactory,
            new IntervalSchedule(TimeSpan.FromDays(1)),
            new InMemoryDistributedLockProvider(),
            timeProvider,
            logger)
    {
        public bool HasRun { get; private set; }

        protected override string ScheduleKey => "nightly-work";
        protected override string ServiceName => nameof(NightlyWorkBackgroundService);

        public Task RunOnce()
        {
            return ExecuteScheduledTask(CancellationToken.None);
        }

        /// <summary>
        ///     Gives the service's background loop a moment of real time to act on a clock that just moved.
        ///     Short and bounded: the loop only has to notice the advance and run a synchronous check.
        /// </summary>
        public async Task WaitForIdle()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5));
        }

        protected override Task ExecuteScheduledTask(CancellationToken stoppingToken)
        {
            using var scope = ServiceScopeFactory.CreateScope();
            var scopedWork = scope.ServiceProvider.GetRequiredService<ScopedWork>();
            scopedWork.Run();
            HasRun = scopedWork.HasRun;

            return Task.CompletedTask;
        }
    }
}
