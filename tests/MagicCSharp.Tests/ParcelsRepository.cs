using MagicCSharp.Data.Repositories;
using MagicCSharp.Testing;

namespace MagicCSharp.Tests;

public interface IParcelsRepository : IRepository<Parcel, long, ParcelEdit, ParcelFilter>, IPaginatedRepository<Parcel, ParcelFilter>,
    ISoftDeleteRepository<Parcel, long, ParcelFilter>;

/// <summary>Parcels in memory; a parcel never moves to another warehouse.</summary>
public class ParcelsRepository(TimeProvider timeProvider, long firstId = 1)
    : InMemorySoftDeleteRepository<Parcel, ParcelEdit, ParcelFilter>(timeProvider, firstId), IParcelsRepository
{
    protected override IReadOnlyCollection<string> SetOncePropertyNames => [nameof(Parcel.WarehouseId)];
}
