using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.EntityFramework.Tests.Fixtures;
using MagicCSharp.Data.Utils;
using MagicCSharp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>
///     The generated query and the generated <c>Matches</c> of one filter select the same shipments: each case asks
///     the database and the stored shipments in memory with the same filter, and both must return exactly the
///     shipments the case expects.
/// </summary>
public class FilterParityTests : RepositoryTestBase
{
    private static readonly DateTimeOffset FirstDelivery = new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.FromHours(-8));

    private static readonly DateTimeOffset SecondDelivery = new DateTimeOffset(2026, 2, 3, 9, 0, 0, TimeSpan.FromHours(-8));

    private ShipmentsRepository Shipments => new ShipmentsRepository(new RepositoryServices<TestDbContext>(DbContextFactory, KeyGen, TimeProvider, NullLoggerFactory.Instance));

    [Fact]
    public async Task Ids_Null_SelectsEveryShipment()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { Ids = null }, stored.All);
    }

    [Fact]
    public async Task Ids_Empty_SelectsNoShipment()
    {
        await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { Ids = [] });
    }

    [Fact]
    public async Task Ids_One_SelectsThatShipment()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { Ids = [stored.Booked.Id] }, stored.Booked);
    }

    [Fact]
    public async Task Ids_Several_SelectsThoseShipments()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { Ids = [stored.Booked.Id, stored.Delivered.Id] }, stored.Booked, stored.Delivered);
    }

    [Fact]
    public async Task Statuses_Null_SelectsEveryShipment()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { Statuses = null }, stored.All);
    }

    [Fact]
    public async Task Statuses_Empty_SelectsNoShipment()
    {
        await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { Statuses = [] });
    }

    [Fact]
    public async Task Statuses_One_SelectsShipmentsInThatStatus()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { Statuses = [ShipmentStatus.Delivered] }, stored.Delivered, stored.OtherTenants);
    }

    [Fact]
    public async Task TrackingCodes_Null_SelectsEveryShipment()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { TrackingCodes = null }, stored.All);
    }

    [Fact]
    public async Task TrackingCodes_Empty_SelectsNoShipment()
    {
        await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { TrackingCodes = [] });
    }

    [Fact]
    public async Task TrackingCodes_Several_SelectsThoseShipments()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { TrackingCodes = ["TRK-100", "TRK-200"] }, stored.Booked, stored.InTransit);
    }

    [Fact]
    public async Task TrackingCodes_DifferingOnlyInCase_SelectNoShipment()
    {
        // The delivered shipment's code is stored as 'trk-300'.
        await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { TrackingCodes = ["TRK-300"] });
    }

    [Fact]
    public async Task CarrierIds_Null_SelectsEveryShipment_WithAndWithoutACarrier()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { CarrierIds = null }, stored.All);
    }

    [Fact]
    public async Task CarrierIds_Empty_SelectsNoShipment()
    {
        await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { CarrierIds = [] });
    }

    [Fact]
    public async Task CarrierIds_Several_SkipsShipmentsWithoutACarrier()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { CarrierIds = [11, 12] }, stored.Booked, stored.Delivered, stored.OtherTenants, stored.Deleted);
    }

    [Fact]
    public async Task TenantId_Set_SelectsThatTenantsShipments()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { TenantId = 8 }, stored.OtherTenants);
    }

    [Fact]
    public async Task TenantId_Null_SelectsEveryShipment()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { TenantId = null }, stored.All);
    }

    [Fact]
    public async Task CarrierId_Set_SkipsShipmentsWithoutACarrier()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { CarrierId = 11 }, stored.Booked, stored.OtherTenants);
    }

    [Fact]
    public async Task IsDelivered_True_SelectsShipmentsWithADeliveryTime()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { IsDelivered = true }, stored.Delivered, stored.OtherTenants);
    }

    [Fact]
    public async Task IsDelivered_False_SelectsShipmentsWithoutADeliveryTime()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { IsDelivered = false }, stored.Booked, stored.InTransit, stored.Deleted);
    }

    [Fact]
    public async Task IsDelivered_Null_SelectsEveryShipment()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { IsDelivered = null }, stored.All);
    }

    [Fact]
    public async Task IsDeleted_False_SelectsLiveShipments()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { IsDeleted = false }, stored.Booked, stored.InTransit, stored.Delivered, stored.OtherTenants);
    }

    [Fact]
    public async Task Created_StartOnly_SelectsShipmentsFromThatTime()
    {
        var stored = await StoreShipments();
        var range = new ComparableRange<DateTimeOffset> { Start = stored.Delivered.Created };

        await AssertBothSelect(new ShipmentFilter { Created = range }, stored.Delivered, stored.OtherTenants, stored.Deleted);
    }

    [Fact]
    public async Task Created_EndOnly_SelectsShipmentsUpToThatTime()
    {
        var stored = await StoreShipments();
        var range = new ComparableRange<DateTimeOffset> { End = stored.InTransit.Created };

        await AssertBothSelect(new ShipmentFilter { Created = range }, stored.Booked, stored.InTransit);
    }

    [Fact]
    public async Task Created_BothEnds_SelectsShipmentsInBetween()
    {
        var stored = await StoreShipments();
        var range = new ComparableRange<DateTimeOffset> { Start = stored.InTransit.Created, End = stored.OtherTenants.Created };

        await AssertBothSelect(new ShipmentFilter { Created = range }, stored.InTransit, stored.Delivered, stored.OtherTenants);
    }

    [Fact]
    public async Task Created_ExclusiveBounds_LeaveOutShipmentsOnTheBounds()
    {
        var stored = await StoreShipments();
        var range = new ComparableRange<DateTimeOffset>
        {
            Start = stored.InTransit.Created,
            End = stored.OtherTenants.Created,
            IsStartInclusive = false,
            IsEndInclusive = false,
        };

        await AssertBothSelect(new ShipmentFilter { Created = range }, stored.Delivered);
    }

    [Fact]
    public async Task Created_StartAfterEnd_SelectsNoShipment()
    {
        var stored = await StoreShipments();
        var range = new ComparableRange<DateTimeOffset> { Start = stored.OtherTenants.Created, End = stored.InTransit.Created };

        await AssertBothSelect(new ShipmentFilter { Created = range });
    }

    [Fact]
    public async Task Created_BoundInAnotherOffset_ComparesTheSameInstant()
    {
        var stored = await StoreShipments();
        var startInTokyo = stored.Delivered.Created.ToOffset(TimeSpan.FromHours(9));
        var range = new ComparableRange<DateTimeOffset> { Start = startInTokyo };

        await AssertBothSelect(new ShipmentFilter { Created = range }, stored.Delivered, stored.OtherTenants, stored.Deleted);
    }

    [Fact]
    public async Task DeliveredRange_StartOnly_SkipsShipmentsWithoutADeliveryTime()
    {
        var stored = await StoreShipments();
        var range = new ComparableRange<DateTimeOffset> { Start = FirstDelivery };

        await AssertBothSelect(new ShipmentFilter { DeliveredRange = range }, stored.Delivered, stored.OtherTenants);
    }

    [Fact]
    public async Task DeliveredRange_EndOnly_SkipsShipmentsWithoutADeliveryTime()
    {
        var stored = await StoreShipments();
        var range = new ComparableRange<DateTimeOffset> { End = FirstDelivery };

        await AssertBothSelect(new ShipmentFilter { DeliveredRange = range }, stored.Delivered);
    }

    [Fact]
    public async Task DeliveredRange_BothEnds_SelectsDeliveriesInBetween()
    {
        var stored = await StoreShipments();
        var range = new ComparableRange<DateTimeOffset> { Start = FirstDelivery.AddHours(1), End = SecondDelivery };

        await AssertBothSelect(new ShipmentFilter { DeliveredRange = range }, stored.OtherTenants);
    }

    [Fact]
    public async Task DeliveredRange_NeitherEnd_SelectsEveryShipment_WithAndWithoutADeliveryTime()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { DeliveredRange = new ComparableRange<DateTimeOffset>() }, stored.All);
    }

    [Fact]
    public async Task TwoFields_SelectShipmentsThatPassBoth()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { TenantId = 7, Statuses = [ShipmentStatus.Booked] }, stored.Booked, stored.Deleted);
    }

    [Fact]
    public async Task CustomField_SelectsWhatItsHandWrittenQueryAndMatchSelect()
    {
        var stored = await StoreShipments();

        await AssertBothSelect(new ShipmentFilter { TrackingCodeStartsWith = "TRK-1" }, stored.Booked);
    }

    // The same filter through the generated query in the database and the generated Matches over every stored
    // shipment; both must select exactly the expected ones.
    private async Task AssertBothSelect(ShipmentFilter filter, params Shipment[] expected)
    {
        var expectedIds = expected.Select(shipment => shipment.Id).Order().ToArray();
        var everyShipment = await Shipments.Get(new ShipmentFilter());

        var databaseIds = (await Shipments.Get(filter)).Select(shipment => shipment.Id).Order().ToArray();
        var matchedIds = everyShipment.Where(filter.Matches).Select(shipment => shipment.Id).Order().ToArray();

        Assert.Equal(5, everyShipment.Count);
        Assert.Equal(expectedIds, databaseIds);
        Assert.Equal(expectedIds, matchedIds);
    }

    private async Task<StoredShipments> StoreShipments()
    {
        var booked = await StoreShipment(AShipment(7, ShipmentStatus.Booked, 11, "TRK-100"));
        var inTransit = await StoreShipment(AShipment(7, ShipmentStatus.InTransit, null, "TRK-200"));
        var delivered = await StoreShipment(AShipment(7, ShipmentStatus.Delivered, 12, "trk-300") with { Delivered = FirstDelivery });
        var otherTenants = await StoreShipment(AShipment(8, ShipmentStatus.Delivered, 11, "TRK-400") with { Delivered = SecondDelivery });
        var deleted = await StoreShipment(AShipment(7, ShipmentStatus.Booked, 12, "TRK-500"));
        deleted = await Shipments.SoftDelete(deleted.Id);

        return new StoredShipments
        {
            Booked = booked,
            InTransit = inTransit,
            Delivered = delivered,
            OtherTenants = otherTenants,
            Deleted = deleted,
        };
    }

    // One hour apart, so each shipment has its own created time for the range cases.
    private async Task<Shipment> StoreShipment(ShipmentEdit edit)
    {
        TimeProvider.Advance(TimeSpan.FromHours(1));
        return await Shipments.Create(edit);
    }

    private static ShipmentEdit AShipment(long tenantId, ShipmentStatus status, long? carrierId, string trackingCode)
    {
        return new ShipmentEdit
        {
            TenantId = tenantId,
            Status = status,
            CarrierId = carrierId,
            TrackingCode = trackingCode,
        };
    }

    private record StoredShipments
    {
        public required Shipment Booked { get; init; }
        public required Shipment InTransit { get; init; }
        public required Shipment Delivered { get; init; }
        public required Shipment OtherTenants { get; init; }
        public required Shipment Deleted { get; init; }

        public Shipment[] All => [Booked, InTransit, Delivered, OtherTenants, Deleted];
    }
}
