using MagicCSharp.Data.Repositories;
using MagicCSharp.Testing;

namespace MagicCSharp.Tests;

public interface IParcelsRepository : IRepository<Parcel, long, ParcelEdit, ParcelFilter>, IPaginatedRepository<Parcel, ParcelFilter>,
    ISoftDeleteRepository<Parcel, long, ParcelFilter>, ITenantScopedDestroyer;

/// <summary>Parcels in memory; the warehouse is the tenant.</summary>
public class ParcelsRepository(TimeProvider timeProvider, long firstId = 1)
    : InMemorySoftDeleteRepository<Parcel, ParcelEdit, ParcelFilter>(timeProvider, firstId), IParcelsRepository
{
    protected override string? TenantKeyPropertyName => nameof(Parcel.WarehouseId);
}
