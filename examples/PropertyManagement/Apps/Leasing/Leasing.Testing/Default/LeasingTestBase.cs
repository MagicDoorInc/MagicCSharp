using Acme.Leasing.Data.EntityFramework;
using Acme.Leasing.Domains.Charges.LateFees.Models.Entities;
using Acme.Leasing.Domains.Charges.LateFees.UseCases;
using Acme.Leasing.Domains.Leases.Models.Entities;
using Acme.Leasing.Domains.Leases.UseCases;
using MagicCSharp.Events.Events;
using MagicCSharp.Events;
using MagicCSharp.Infrastructure.KeyGen;
using MagicCSharp.Modules;
using MagicCSharp.Testing.Database;
using MagicCSharp.Testing;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Leasing.Testing;

/// <summary>
///     The service as it runs, minus the host: real use cases, real repositories against this test class's own
///     PostgreSQL database, real event handlers. Three things are swapped, each for a reason:
///     <list type="bullet">
///         <item>the clock is a <see cref="FakeTimeProvider" />, so a test moves time instead of waiting for it;</item>
///         <item>
///             events go through <see cref="SyncEventDispatcher" />, which runs handlers before
///             <c>Dispatch</c> returns, so a test can assert on what they did;
///         </item>
///         <item>locks are in-memory, so tests need no lock directory.</item>
///     </list>
/// </summary>
public abstract class LeasingTestBase : TestRepositoryBase<MagicLeasingContext>
{
    private IServiceScope? scope;

    protected SyncEventDispatcher EventDispatcher => Resolve<SyncEventDispatcher>();

    protected T Resolve<T>()
        where T : notnull
    {
        scope ??= BuildServiceProvider().CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>The schema comes from the migrations, so every test run also proves they produce a working database.</summary>
    protected override Task InitializeDatabase(MagicLeasingContext context)
    {
        return context.Database.MigrateAsync();
    }

    protected async Task<Property> CreateProperty(string timeZoneId = "America/Los_Angeles")
    {
        var createProperty = Resolve<ICreatePropertyUseCase>();

        return await createProperty.Execute(new CreatePropertyRequest
        {
            Name = "Maple Court",
            Address = "12 Maple Court, Portland, OR",
            TimeZoneId = timeZoneId,
        });
    }

    protected async Task<SignLeaseResult> SignLease(Property property, DateOnly startDate)
    {
        var signLease = Resolve<ISignLeaseUseCase>();

        return await signLease.Execute(new SignLeaseRequest
        {
            PropertyId = property.Id,
            TenantName = "Dana Whitfield",
            TenantEmail = "dana@example.com",
            MonthlyRent = 1850m,
            SecurityDeposit = 1850m,
            StartDate = startDate,
            EndDate = startDate.AddYears(1),
        });
    }

    protected async Task<LateFeePolicy> SetLateFeePolicy(Property property, int graceDays, decimal amount)
    {
        var setLateFeePolicy = Resolve<ISetLateFeePolicyUseCase>();

        return await setLateFeePolicy.Execute(property.Id, new SetLateFeePolicyRequest
        {
            GraceDays = graceDays,
            Amount = amount,
        });
    }

    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // The repositories exactly as the service registers them. The connection settings only satisfy the
        // registration; the context factory they configure is replaced straight after by this class's own.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_HOST"] = "replaced-by-test-database",
                ["DB_PORT"] = "5432",
                ["DB_NAME"] = "replaced-by-test-database",
                ["DB_USER"] = "replaced-by-test-database",
                ["DB_PASSWORD"] = "replaced-by-test-database",
                ["DB_VERIFY_CONNECTION"] = "false",
            })
            .Build();
        services.AddLeasingRepositories(configuration);
        services.AddSingleton(DbContextFactory);

        services.AddMagicCSharp();
        services.AddMagicEvents();
        services.AddSingleton<TimeProvider>(TimeProvider);
        services.AddSingleton<IKeyGenService>(KeyGen);
        services.AddSingleton<IDistributedLockProvider, InMemoryDistributedLockProvider>();
        services.AddSingleton<SyncEventDispatcher>();
        services.AddSingleton<IEventDispatcher>(provider => provider.GetRequiredService<SyncEventDispatcher>());

        return services.BuildServiceProvider();
    }
}
