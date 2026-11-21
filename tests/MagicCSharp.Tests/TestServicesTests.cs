using System.Reflection;
using MagicCSharp.Infrastructure.Exceptions;
using MagicCSharp.Modules;
using MagicCSharp.Testing;
using MagicCSharp.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace MagicCSharp.Tests;

/// <summary>
///     <see cref="TestServices" /> builds use cases found by the app's own discovery, with the fixture's fakes, the
///     test's clock and silent loggers, and lets one instance take a different fake.
/// </summary>
public class TestServicesTests
{
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider timeProvider = new FakeTimeProvider(Start);

    [Fact]
    public async Task Get_UseCase_BuiltWithAddedFakesAndTestClock()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = parcelsRepository.SeedStoredRow(ParcelEdit.Received("Blue box"));
        var testServices = ParcelTestServices(parcelsRepository);
        timeProvider.Advance(TimeSpan.FromHours(1));

        var shelvedParcel = await testServices.Get<IShelveParcelUseCase>().Execute(parcel.Id);

        Assert.Equal(ParcelStatus.Shelved, shelvedParcel.Status);
        Assert.Equal(Start.AddHours(1), shelvedParcel.Shelved);
        Assert.Equal(shelvedParcel, await parcelsRepository.Get(parcel.Id));
    }

    [Fact]
    public void Get_UseCaseLogger_IsNullLogger()
    {
        var testServices = ParcelTestServices(new ParcelsRepository(timeProvider));

        var logger = testServices.Get<ILogger<ShelveParcelUseCase>>();

        Assert.IsType<NullLogger<ShelveParcelUseCase>>(logger);
    }

    [Fact]
    public void Get_MissingFake_ThrowsNamingIt()
    {
        var testServices = new TestServices(timeProvider).AddRegistrations(AddParcelUseCases);

        var exception = Assert.Throws<InvalidOperationException>(() => testServices.Get<IShelveParcelUseCase>());

        Assert.Contains(nameof(IParcelsRepository), exception.Message);
    }

    [Fact]
    public void AddFake_Twice_LaterOneIsResolved()
    {
        var laterParcelsRepository = new ParcelsRepository(timeProvider);
        var testServices = ParcelTestServices(new ParcelsRepository(timeProvider)).AddFake<IParcelsRepository>(laterParcelsRepository);

        var parcelsRepository = testServices.Get<IParcelsRepository>();

        Assert.Same(laterParcelsRepository, parcelsRepository);
    }

    [Fact]
    public void AddFake_AfterFirstGet_Throws()
    {
        var testServices = ParcelTestServices(new ParcelsRepository(timeProvider));
        testServices.Get<IShelveParcelUseCase>();

        Assert.Throws<InvalidOperationException>(() => testServices.AddFake<IParcelsRepository>(new ParcelsRepository(timeProvider)));
    }

    [Fact]
    public void AddRegistrations_AfterFirstGet_Throws()
    {
        var testServices = ParcelTestServices(new ParcelsRepository(timeProvider));
        testServices.Get<IShelveParcelUseCase>();

        Assert.Throws<InvalidOperationException>(() => testServices.AddRegistrations(AddParcelUseCases));
    }

    [Fact]
    public async Task Create_TwoDependencies_EachGoesToItsParameter()
    {
        var registeredParcelsRepository = new ParcelsRepository(timeProvider);
        var registeredParcel = registeredParcelsRepository.SeedStoredRow(ParcelEdit.Received("Registered box"));
        var otherParcelsRepository = new ParcelsRepository(timeProvider);
        otherParcelsRepository.SeedStoredRow(ParcelEdit.Received("Other box"));
        var otherFindParcel = new FindParcelUseCase(otherParcelsRepository);
        var testServices = ParcelTestServices(registeredParcelsRepository);

        var shelveParcel = testServices.Create<ShelveParcelUseCase>(otherFindParcel, otherParcelsRepository);
        var shelvedParcel = await shelveParcel.Execute(registeredParcel.Id);

        Assert.Equal("Other box", shelvedParcel.Label);
        Assert.Equal(ParcelStatus.Shelved, shelvedParcel.Status);
        Assert.Equal(registeredParcel, await registeredParcelsRepository.Get(registeredParcel.Id));
    }

    [Fact]
    public async Task Create_WithOtherFake_NestedUseCaseKeepsRegisteredFake()
    {
        var registeredParcelsRepository = new ParcelsRepository(timeProvider);
        var registeredParcel = registeredParcelsRepository.SeedStoredRow(ParcelEdit.Received("Registered box"));
        var otherParcelsRepository = new ParcelsRepository(timeProvider);
        otherParcelsRepository.SeedStoredRow(ParcelEdit.Received("Other box"));
        var testServices = ParcelTestServices(registeredParcelsRepository);

        var shelveParcel = testServices.Create<ShelveParcelUseCase>(otherParcelsRepository);
        var shelvedParcel = await shelveParcel.Execute(registeredParcel.Id);

        Assert.Equal("Registered box", shelvedParcel.Label);
        Assert.Equal(shelvedParcel, await otherParcelsRepository.Get(registeredParcel.Id));
        Assert.Equal(registeredParcel, await registeredParcelsRepository.Get(registeredParcel.Id));
    }

    [Fact]
    public async Task Create_WithOtherFake_GetStillReturnsRegisteredFake()
    {
        var registeredParcelsRepository = new ParcelsRepository(timeProvider);
        var registeredParcel = registeredParcelsRepository.SeedStoredRow(ParcelEdit.Received("Registered box"));
        var otherParcelsRepository = new ParcelsRepository(timeProvider);
        otherParcelsRepository.SeedStoredRow(ParcelEdit.Received("Other box"));
        var testServices = ParcelTestServices(registeredParcelsRepository);
        testServices.Create<ShelveParcelUseCase>(otherParcelsRepository);

        var shelvedParcel = await testServices.Get<IShelveParcelUseCase>().Execute(registeredParcel.Id);

        Assert.Equal(shelvedParcel, await registeredParcelsRepository.Get(registeredParcel.Id));
        Assert.Equal(ParcelStatus.Received, (await otherParcelsRepository.Get(registeredParcel.Id))?.Status);
    }

    private TestServices ParcelTestServices(ParcelsRepository parcelsRepository)
    {
        return new TestServices(timeProvider)
            .AddFake<IParcelsRepository>(parcelsRepository)
            .AddRegistrations(AddParcelUseCases);
    }

    private static void AddParcelUseCases(IServiceCollection services)
    {
        services.AddMagicUseCases(ThisAssemblyOnly);
    }

    private static bool ThisAssemblyOnly(Assembly assembly)
    {
        return assembly == typeof(TestServicesTests).Assembly;
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────────────────

    public interface IFindParcelUseCase : IMagicUseCase
    {
        Task<Parcel> Execute(long parcelId);
    }

    public class FindParcelUseCase(IParcelsRepository parcelsRepository) : IFindParcelUseCase
    {
        public async Task<Parcel> Execute(long parcelId)
        {
            var parcel = await parcelsRepository.Get(parcelId);
            NotFoundException.ThrowIfNull(parcel, parcelId);

            return parcel;
        }
    }

    public interface IShelveParcelUseCase : IMagicUseCase
    {
        Task<Parcel> Execute(long parcelId);
    }

    /// <summary>Finds the parcel, then writes it back shelved, stamped with the clock's now.</summary>
    public class ShelveParcelUseCase(
        IParcelsRepository parcelsRepository,
        IFindParcelUseCase findParcel,
        TimeProvider timeProvider,
        ILogger<ShelveParcelUseCase> logger) : IShelveParcelUseCase
    {
        public async Task<Parcel> Execute(long parcelId)
        {
            logger.LogInformation("Shelving parcel {ParcelId}", parcelId);
            var parcel = await findParcel.Execute(parcelId);

            return await parcelsRepository.Update(parcelId, parcel with { Status = ParcelStatus.Shelved, Shelved = timeProvider.GetUtcNow() });
        }
    }
}
