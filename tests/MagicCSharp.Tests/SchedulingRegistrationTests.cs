using MagicCSharp.Scheduling;
using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MagicCSharp.Tests;

public class SchedulingRegistrationTests
{
    [Fact]
    public void AddMagicScheduling_supplies_everything_a_scheduled_service_resolves()
    {
        // ScheduledBackgroundService does GetRequiredService for both of these. Before this registration
        // existed, the package advertised drift-free scheduling and threw the moment you used it.
        var services = new ServiceCollection();

        services.AddMagicScheduling();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IScheduleStore>());
        Assert.NotNull(provider.GetService<IDistributedLockProvider>());
    }

    [Fact]
    public void Your_own_schedule_store_is_not_replaced()
    {
        // The defaults are single-machine. Anyone deploying more than one instance registers their own, and
        // the convenience method must not quietly win over it.
        var services = new ServiceCollection();
        var mine = new InMemoryScheduleStore();

        services.AddSingleton<IScheduleStore>(mine);
        services.AddMagicScheduling();

        using var provider = services.BuildServiceProvider();

        Assert.Same(mine, provider.GetRequiredService<IScheduleStore>());
    }

    [Fact]
    public void Registering_twice_does_not_produce_two_stores()
    {
        var services = new ServiceCollection();

        services.AddMagicScheduling();
        services.AddMagicScheduling();

        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<IScheduleStore>());
    }
}
