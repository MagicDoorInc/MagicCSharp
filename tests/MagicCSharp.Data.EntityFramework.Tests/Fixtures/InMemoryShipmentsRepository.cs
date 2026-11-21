using MagicCSharp.Testing;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>
///     Shipments in memory, the twin of <see cref="ShipmentsRepository" />; the tenant column is set once, as the
///     <c>required</c> <see cref="ShipmentDal.TenantId" /> is.
/// </summary>
public class InMemoryShipmentsRepository(TimeProvider timeProvider) : InMemorySoftDeleteRepository<Shipment, ShipmentEdit, ShipmentFilter>(timeProvider)
{
    protected override IReadOnlyCollection<string> SetOncePropertyNames => [nameof(Shipment.TenantId)];
}
