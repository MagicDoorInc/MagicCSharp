using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.EntityFramework.Tests.Fixtures;
using MagicCSharp.Data.Models;
using MagicCSharp.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>
///     The in-memory repository a functional test uses answers as the database repository does: each case makes the
///     same calls on <see cref="ShipmentsRepository" /> in PostgreSQL and on <see cref="InMemoryShipmentsRepository" />,
///     on the same clock, and both must give the same rows and stamps. Ids differ (the database's come from the key
///     generator), so rows are compared without them.
/// </summary>
public class InMemoryRepositoryParityTests : RepositoryTestBase
{
    private const long Tenant = 7;

    private const long OtherTenant = 8;

    private readonly InMemoryShipmentsRepository inMemoryShipmentsRepository;

    public InMemoryRepositoryParityTests()
    {
        inMemoryShipmentsRepository = new InMemoryShipmentsRepository(TimeProvider);
    }

    private ShipmentsRepository Shipments => new ShipmentsRepository(new RepositoryServices<TestDbContext>(DbContextFactory, KeyGen, TimeProvider, NullLoggerFactory.Instance));

    [Fact]
    public async Task Create_SameRowAndStamps()
    {
        TimeProvider.Advance(TimeSpan.FromHours(1));

        var stored = await StoreInBoth(AShipment(Tenant, "TRK-100") with { Delivered = new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.FromHours(-8)) });

        Assert.Equal(WithoutId(await Shipments.Get(stored.DatabaseShipment.Id)), WithoutId(await inMemoryShipmentsRepository.Get(stored.InMemoryShipment.Id)));
        Assert.Equal(TimeProvider.GetUtcNow(), stored.InMemoryShipment.Created);
    }

    [Fact]
    public async Task GetByFilter_SameRowsSameOrder()
    {
        await StoreInBoth(AShipment(Tenant, "TRK-100"));
        await StoreInBoth(AShipment(OtherTenant, "TRK-200"));
        await StoreInBoth(AShipment(Tenant, "TRK-300"));
        var tenantShipmentFilter = new ShipmentFilter { TenantId = Tenant };

        var databaseShipments = await Shipments.Get(tenantShipmentFilter);
        var inMemoryShipments = await inMemoryShipmentsRepository.Get(tenantShipmentFilter);

        Assert.Equal(new[] { "TRK-300", "TRK-100" }, databaseShipments.Select(shipment => shipment.TrackingCode));
        Assert.Equal(databaseShipments.Select(WithoutId), inMemoryShipments.Select(WithoutId));
    }

    [Fact]
    public async Task GetPage_SameItemsAndTotal()
    {
        await StoreInBoth(AShipment(Tenant, "TRK-100"));
        await StoreInBoth(AShipment(Tenant, "TRK-200"));
        await StoreInBoth(AShipment(Tenant, "TRK-300"));
        var paginationRequest = new PaginationRequest(2, 2);

        var databasePage = await Shipments.Get(paginationRequest, new ShipmentFilter());
        var inMemoryPage = await inMemoryShipmentsRepository.Get(paginationRequest, new ShipmentFilter());

        Assert.Equal(3, databasePage.TotalCount);
        Assert.Equal(databasePage.TotalCount, inMemoryPage.TotalCount);
        Assert.Equal(databasePage.Items.Select(WithoutId), inMemoryPage.Items.Select(WithoutId));
    }

    [Fact]
    public async Task Update_Changed_SameValuesAndUpdatedStamp()
    {
        var stored = await StoreInBoth(AShipment(Tenant, "TRK-100"));
        TimeProvider.Advance(TimeSpan.FromHours(1));
        var shipmentEdit = AShipment(Tenant, "TRK-100") with { Status = ShipmentStatus.InTransit, CarrierId = 11 };

        await Shipments.Update(stored.DatabaseShipment.Id, shipmentEdit);
        await inMemoryShipmentsRepository.Update(stored.InMemoryShipment.Id, shipmentEdit);

        var databaseShipment = await Shipments.Get(stored.DatabaseShipment.Id);
        Assert.Equal(TimeProvider.GetUtcNow(), databaseShipment?.Updated);
        Assert.Equal(WithoutId(databaseShipment), WithoutId(await inMemoryShipmentsRepository.Get(stored.InMemoryShipment.Id)));
    }

    [Fact]
    public async Task Update_Unchanged_BothKeepUpdated()
    {
        var stored = await StoreInBoth(AShipment(Tenant, "TRK-100"));
        var created = TimeProvider.GetUtcNow();
        TimeProvider.Advance(TimeSpan.FromHours(1));

        await Shipments.Update(stored.DatabaseShipment.Id, AShipment(Tenant, "TRK-100"));
        await inMemoryShipmentsRepository.Update(stored.InMemoryShipment.Id, AShipment(Tenant, "TRK-100"));

        Assert.Equal(created, (await Shipments.Get(stored.DatabaseShipment.Id))?.Updated);
        Assert.Equal(created, (await inMemoryShipmentsRepository.Get(stored.InMemoryShipment.Id))?.Updated);
    }

    [Fact]
    public async Task Update_MissingId_BothThrowNotFound()
    {
        await Assert.ThrowsAsync<NotFoundIdException>(() => Shipments.Update(424242, AShipment(Tenant, "TRK-100")));
        await Assert.ThrowsAsync<NotFoundIdException>(() => inMemoryShipmentsRepository.Update(424242, AShipment(Tenant, "TRK-100")));
    }

    [Fact]
    public async Task Delete_MissingId_BothThrowNotFound()
    {
        await Assert.ThrowsAsync<NotFoundIdException>(() => Shipments.Delete(424242));
        await Assert.ThrowsAsync<NotFoundIdException>(() => inMemoryShipmentsRepository.Delete(424242));
    }

    [Fact]
    public async Task SoftDelete_SameDeletedAndUpdated()
    {
        var stored = await StoreInBoth(AShipment(Tenant, "TRK-100"));
        TimeProvider.Advance(TimeSpan.FromHours(1));

        await Shipments.SoftDelete(stored.DatabaseShipment.Id);
        await inMemoryShipmentsRepository.SoftDelete(stored.InMemoryShipment.Id);

        var databaseShipment = await Shipments.Get(stored.DatabaseShipment.Id);
        Assert.Equal(TimeProvider.GetUtcNow(), databaseShipment?.Deleted);
        Assert.Equal(WithoutId(databaseShipment), WithoutId(await inMemoryShipmentsRepository.Get(stored.InMemoryShipment.Id)));
    }

    [Fact]
    public async Task UpdateSeveral_SameChangedRowCountAndStamps()
    {
        var changing = await StoreInBoth(AShipment(Tenant, "TRK-100"));
        var unchanged = await StoreInBoth(AShipment(Tenant, "TRK-200"));
        var created = TimeProvider.GetUtcNow();
        TimeProvider.Advance(TimeSpan.FromHours(1));
        var changedEdit = AShipment(Tenant, "TRK-100") with { Status = ShipmentStatus.InTransit };

        var databaseChangedCount = await Shipments.Update(new Dictionary<long, ShipmentEdit>
        {
            [changing.DatabaseShipment.Id] = changedEdit,
            [unchanged.DatabaseShipment.Id] = AShipment(Tenant, "TRK-200"),
        });
        var inMemoryChangedCount = await inMemoryShipmentsRepository.Update(new Dictionary<long, ShipmentEdit>
        {
            [changing.InMemoryShipment.Id] = changedEdit,
            [unchanged.InMemoryShipment.Id] = AShipment(Tenant, "TRK-200"),
        });

        Assert.Equal(1, databaseChangedCount);
        Assert.Equal(databaseChangedCount, inMemoryChangedCount);
        var databaseShipments = await Shipments.Get(new ShipmentFilter());
        Assert.Equal(TimeProvider.GetUtcNow(), databaseShipments.Single(shipment => shipment.TrackingCode == "TRK-100").Updated);
        Assert.Equal(created, databaseShipments.Single(shipment => shipment.TrackingCode == "TRK-200").Updated);
        Assert.Equal(databaseShipments.Select(WithoutId), (await inMemoryShipmentsRepository.Get(new ShipmentFilter())).Select(WithoutId));
    }

    [Fact]
    public async Task UpdateSeveralEntities_SameChangedRowCountAndStamps()
    {
        var changing = await StoreInBoth(AShipment(Tenant, "TRK-100"));
        var unchanged = await StoreInBoth(AShipment(Tenant, "TRK-200"));
        var created = TimeProvider.GetUtcNow();
        TimeProvider.Advance(TimeSpan.FromHours(1));

        var databaseChangedCount = await Shipments.Update([changing.DatabaseShipment with { Status = ShipmentStatus.InTransit }, unchanged.DatabaseShipment]);
        var inMemoryChangedCount = await inMemoryShipmentsRepository.Update([changing.InMemoryShipment with { Status = ShipmentStatus.InTransit }, unchanged.InMemoryShipment]);

        Assert.Equal(1, databaseChangedCount);
        Assert.Equal(databaseChangedCount, inMemoryChangedCount);
        var databaseShipments = await Shipments.Get(new ShipmentFilter());
        Assert.Equal(TimeProvider.GetUtcNow(), databaseShipments.Single(shipment => shipment.TrackingCode == "TRK-100").Updated);
        Assert.Equal(created, databaseShipments.Single(shipment => shipment.TrackingCode == "TRK-200").Updated);
        Assert.Equal(databaseShipments.Select(WithoutId), (await inMemoryShipmentsRepository.Get(new ShipmentFilter())).Select(WithoutId));
    }

    [Fact]
    public async Task UpdateSeveral_OneMissing_BothThrowNotFoundAndWriteNothing()
    {
        var stored = await StoreInBoth(AShipment(Tenant, "TRK-100"));
        var changedEdit = AShipment(Tenant, "TRK-100") with { Status = ShipmentStatus.InTransit };

        await Assert.ThrowsAsync<NotFoundIdException>(() => Shipments.Update(new Dictionary<long, ShipmentEdit>
        {
            [stored.DatabaseShipment.Id] = changedEdit,
            [424242] = changedEdit,
        }));
        await Assert.ThrowsAsync<NotFoundIdException>(() => inMemoryShipmentsRepository.Update(new Dictionary<long, ShipmentEdit>
        {
            [stored.InMemoryShipment.Id] = changedEdit,
            [424242] = changedEdit,
        }));

        var databaseShipments = await Shipments.Get(new ShipmentFilter());
        Assert.Equal(ShipmentStatus.Booked, Assert.Single(databaseShipments).Status);
        Assert.Equal(databaseShipments.Select(WithoutId), (await inMemoryShipmentsRepository.Get(new ShipmentFilter())).Select(WithoutId));
    }

    [Fact]
    public async Task UpdateSeveralEntities_OneMissing_BothThrowNotFoundAndWriteNothing()
    {
        var stored = await StoreInBoth(AShipment(Tenant, "TRK-100"));

        await Assert.ThrowsAsync<NotFoundIdException>(() => Shipments.Update([stored.DatabaseShipment with { Status = ShipmentStatus.InTransit }, stored.DatabaseShipment with { Id = 424242 }]));
        await Assert.ThrowsAsync<NotFoundIdException>(() => inMemoryShipmentsRepository.Update([stored.InMemoryShipment with { Status = ShipmentStatus.InTransit }, stored.InMemoryShipment with { Id = 424242 }]));

        var databaseShipments = await Shipments.Get(new ShipmentFilter());
        Assert.Equal(ShipmentStatus.Booked, Assert.Single(databaseShipments).Status);
        Assert.Equal(databaseShipments.Select(WithoutId), (await inMemoryShipmentsRepository.Get(new ShipmentFilter())).Select(WithoutId));
    }

    [Fact]
    public async Task DeleteSeveral_SameCountAndRemainingRows()
    {
        var first = await StoreInBoth(AShipment(Tenant, "TRK-100"));
        await StoreInBoth(AShipment(Tenant, "TRK-200"));
        var third = await StoreInBoth(AShipment(Tenant, "TRK-300"));

        var databaseDeletedCount = await Shipments.Delete([first.DatabaseShipment.Id, third.DatabaseShipment.Id]);
        var inMemoryDeletedCount = await inMemoryShipmentsRepository.Delete([first.InMemoryShipment.Id, third.InMemoryShipment.Id]);

        Assert.Equal(2, databaseDeletedCount);
        Assert.Equal(databaseDeletedCount, inMemoryDeletedCount);
        var databaseShipments = await Shipments.Get(new ShipmentFilter());
        Assert.Equal(new[] { "TRK-200" }, databaseShipments.Select(shipment => shipment.TrackingCode));
        Assert.Equal(databaseShipments.Select(WithoutId), (await inMemoryShipmentsRepository.Get(new ShipmentFilter())).Select(WithoutId));
    }

    [Fact]
    public async Task DeleteSeveral_OneMissing_BothThrowNotFoundAndDeleteNothing()
    {
        var stored = await StoreInBoth(AShipment(Tenant, "TRK-100"));

        await Assert.ThrowsAsync<NotFoundIdException>(() => Shipments.Delete([stored.DatabaseShipment.Id, 424242]));
        await Assert.ThrowsAsync<NotFoundIdException>(() => inMemoryShipmentsRepository.Delete([stored.InMemoryShipment.Id, 424242]));

        var databaseShipments = await Shipments.Get(new ShipmentFilter());
        Assert.Single(databaseShipments);
        Assert.Equal(databaseShipments.Select(WithoutId), (await inMemoryShipmentsRepository.Get(new ShipmentFilter())).Select(WithoutId));
    }

    [Fact]
    public async Task DeleteByFilter_SameCountAndRemainingRows()
    {
        await StoreInBoth(AShipment(Tenant, "TRK-100"));
        await StoreInBoth(AShipment(OtherTenant, "TRK-200"));
        await StoreInBoth(AShipment(Tenant, "TRK-300"));
        var tenantShipmentFilter = new ShipmentFilter { TenantId = Tenant };

        var databaseDeletedCount = await Shipments.Delete(tenantShipmentFilter);
        var inMemoryDeletedCount = await inMemoryShipmentsRepository.Delete(tenantShipmentFilter);

        Assert.Equal(2, databaseDeletedCount);
        Assert.Equal(databaseDeletedCount, inMemoryDeletedCount);
        var databaseShipments = await Shipments.Get(new ShipmentFilter());
        Assert.Equal(new[] { "TRK-200" }, databaseShipments.Select(shipment => shipment.TrackingCode));
        Assert.Equal(databaseShipments.Select(WithoutId), (await inMemoryShipmentsRepository.Get(new ShipmentFilter())).Select(WithoutId));
    }

    [Fact]
    public async Task SoftDeleteSeveral_SameCountAndStamps()
    {
        var first = await StoreInBoth(AShipment(Tenant, "TRK-100"));
        await StoreInBoth(AShipment(Tenant, "TRK-200"));
        TimeProvider.Advance(TimeSpan.FromHours(1));

        var databaseDeletedCount = await Shipments.SoftDelete([first.DatabaseShipment.Id]);
        var inMemoryDeletedCount = await inMemoryShipmentsRepository.SoftDelete([first.InMemoryShipment.Id]);

        Assert.Equal(1, databaseDeletedCount);
        Assert.Equal(databaseDeletedCount, inMemoryDeletedCount);
        var databaseShipments = await Shipments.Get(new ShipmentFilter());
        Assert.Equal(TimeProvider.GetUtcNow(), databaseShipments.Single(shipment => shipment.TrackingCode == "TRK-100").Deleted);
        Assert.Null(databaseShipments.Single(shipment => shipment.TrackingCode == "TRK-200").Deleted);
        Assert.Equal(databaseShipments.Select(WithoutId), (await inMemoryShipmentsRepository.Get(new ShipmentFilter())).Select(WithoutId));
    }

    [Fact]
    public async Task SoftDeleteSeveral_OneMissing_BothThrowNotFoundAndDeleteNothing()
    {
        var stored = await StoreInBoth(AShipment(Tenant, "TRK-100"));

        await Assert.ThrowsAsync<NotFoundIdException>(() => Shipments.SoftDelete([stored.DatabaseShipment.Id, 424242]));
        await Assert.ThrowsAsync<NotFoundIdException>(() => inMemoryShipmentsRepository.SoftDelete([stored.InMemoryShipment.Id, 424242]));

        var databaseShipments = await Shipments.Get(new ShipmentFilter());
        Assert.Null(Assert.Single(databaseShipments).Deleted);
        Assert.Equal(databaseShipments.Select(WithoutId), (await inMemoryShipmentsRepository.Get(new ShipmentFilter())).Select(WithoutId));
    }

    [Fact]
    public async Task SoftDeleteByFilter_SameCountAndStamps()
    {
        await StoreInBoth(AShipment(Tenant, "TRK-100"));
        await StoreInBoth(AShipment(OtherTenant, "TRK-200"));
        await StoreInBoth(AShipment(Tenant, "TRK-300"));
        TimeProvider.Advance(TimeSpan.FromHours(1));
        var tenantShipmentFilter = new ShipmentFilter { TenantId = Tenant };

        var databaseDeletedCount = await Shipments.SoftDelete(tenantShipmentFilter);
        var inMemoryDeletedCount = await inMemoryShipmentsRepository.SoftDelete(tenantShipmentFilter);

        Assert.Equal(2, databaseDeletedCount);
        Assert.Equal(databaseDeletedCount, inMemoryDeletedCount);
        var databaseShipments = await Shipments.Get(new ShipmentFilter());
        Assert.Equal(new[] { "TRK-300", "TRK-100" }, databaseShipments.Where(shipment => shipment.Deleted == TimeProvider.GetUtcNow()).Select(shipment => shipment.TrackingCode));
        Assert.Null(databaseShipments.Single(shipment => shipment.TrackingCode == "TRK-200").Deleted);
        Assert.Equal(databaseShipments.Select(WithoutId), (await inMemoryShipmentsRepository.Get(new ShipmentFilter())).Select(WithoutId));
    }

    [Fact]
    public async Task DestroyTenant_SameRemainingRows()
    {
        var deleted = await StoreInBoth(AShipment(Tenant, "TRK-100"));
        await Shipments.SoftDelete(deleted.DatabaseShipment.Id);
        await inMemoryShipmentsRepository.SoftDelete(deleted.InMemoryShipment.Id);
        await StoreInBoth(AShipment(Tenant, "TRK-200"));
        await StoreInBoth(AShipment(OtherTenant, "TRK-300"));

        var databaseDestroyedCount = await Shipments.DestroyTenant(Tenant);
        var inMemoryDestroyedCount = await inMemoryShipmentsRepository.DestroyTenant(Tenant);

        Assert.Equal(2, databaseDestroyedCount);
        Assert.Equal(databaseDestroyedCount, inMemoryDestroyedCount);
        var databaseShipments = await Shipments.Get(new ShipmentFilter());
        Assert.Equal(databaseShipments.Select(WithoutId), (await inMemoryShipmentsRepository.Get(new ShipmentFilter())).Select(WithoutId));
    }

    /// <summary>One hour apart, so each shipment has its own created time.</summary>
    private async Task<StoredShipment> StoreInBoth(ShipmentEdit shipmentEdit)
    {
        TimeProvider.Advance(TimeSpan.FromHours(1));

        return new StoredShipment
        {
            DatabaseShipment = await Shipments.Create(shipmentEdit),
            InMemoryShipment = await inMemoryShipmentsRepository.Create(shipmentEdit),
        };
    }

    private static Shipment? WithoutId(Shipment? shipment)
    {
        return shipment == null ? null : shipment with { Id = 0 };
    }

    private static ShipmentEdit AShipment(long tenantId, string trackingCode)
    {
        return new ShipmentEdit
        {
            TenantId = tenantId,
            Status = ShipmentStatus.Booked,
            TrackingCode = trackingCode,
        };
    }

    private record StoredShipment
    {
        public required Shipment DatabaseShipment { get; init; }
        public required Shipment InMemoryShipment { get; init; }
    }
}
