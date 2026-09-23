using Medallion.Threading;
using Medallion.Threading.FileSystem;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Scheduling;

/// <summary>
///     Registers what <see cref="ScheduledBackgroundService" /> resolves at run time.
/// </summary>
public static class SchedulingRegistration
{
    /// <summary>
    ///     The two dependencies a scheduled service needs, with defaults that work on one machine:
    ///     schedule state in memory, and a lock provider backed by the file system.
    ///     <para>
    ///         Both are deliberately single-machine. Replace either for a real deployment — see
    ///         <see cref="InMemoryScheduleStore" /> and <see cref="AddFileSystemDistributedLock" /> for what
    ///         each gives up. Registered with <c>TryAdd</c>, so registering your own first wins.
    ///     </para>
    /// </summary>
    public static IServiceCollection AddMagicScheduling(this IServiceCollection services, string? lockDirectory = null)
    {
        services.AddInMemoryScheduleStore();
        services.AddFileSystemDistributedLock(lockDirectory);

        return services;
    }

    /// <summary>
    ///     Keeps schedule state in memory. Correct for one instance; see
    ///     <see cref="InMemoryScheduleStore" /> for what happens with more than one.
    /// </summary>
    public static IServiceCollection AddInMemoryScheduleStore(this IServiceCollection services)
    {
        services.TryAddSingletonService<IScheduleStore, InMemoryScheduleStore>();
        return services;
    }

    /// <summary>
    ///     A distributed lock backed by lock files in a directory.
    ///     <para>
    ///         Only mutually exclusive between processes that can see the same directory — fine on one
    ///         machine, useless across a cluster unless that directory is genuinely shared. For several
    ///         nodes, register a provider backed by whatever they share: Postgres, Redis, ZooKeeper.
    ///     </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="directory">Where lock files live. Defaults to a folder in the system temp directory.</param>
    public static IServiceCollection AddFileSystemDistributedLock(this IServiceCollection services, string? directory = null)
    {
        var lockDirectory = new DirectoryInfo(directory ?? Path.Combine(Path.GetTempPath(), "magiccsharp-locks"));

        services.TryAddSingletonService<IDistributedLockProvider>(
            new FileDistributedSynchronizationProvider(lockDirectory));

        return services;
    }

    private static void TryAddSingletonService<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TService)))
        {
            services.AddSingleton<TService, TImplementation>();
        }
    }

    private static void TryAddSingletonService<TService>(this IServiceCollection services, TService instance)
        where TService : class
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TService)))
        {
            services.AddSingleton(instance);
        }
    }
}
