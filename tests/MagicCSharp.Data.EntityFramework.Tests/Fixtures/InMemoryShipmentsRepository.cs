using MagicCSharp.Testing;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>Shipments in memory, the twin of <see cref="ShipmentsRepository" />; the tenant column is the one this test assembly's <c>[TenantKey]</c> names.</summary>
public class InMemoryShipmentsRepository(TimeProvider timeProvider) : InMemorySoftDeleteRepository<Shipment, ShipmentEdit, ShipmentFilter>(timeProvider)
{
    protected override string? TenantKeyPropertyName => nameof(Shipment.TenantId);
}
