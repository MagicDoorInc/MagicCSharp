using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Tests;

/// <summary>A parcel in a warehouse, kept by <see cref="ParcelsRepository" /> in the in-memory repository and test services tests.</summary>
public record Parcel : ParcelEdit, IIdEntity, IDeletedEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
    public DateTimeOffset? Deleted { get; init; }

    /// <summary>Not on the edit: written by a statement of its own, so an update must keep it.</summary>
    public string? ScanNote { get; init; }
}

public record ParcelEdit
{
    public const long MainWarehouse = 7;

    public const long OtherWarehouse = 8;

    public required long WarehouseId { get; init; }
    public required ParcelStatus Status { get; init; }
    public required string Label { get; init; }
    public DateTimeOffset? Shelved { get; init; }

    /// <summary>A parcel just received at the main warehouse.</summary>
    public static ParcelEdit Received(string label)
    {
        return new ParcelEdit
        {
            WarehouseId = MainWarehouse,
            Status = ParcelStatus.Received,
            Label = label,
        };
    }
}

public enum ParcelStatus
{
    Received,
    Shelved,
    Shipped,
}

public partial class ParcelFilter : IEntityFilter<Parcel>
{
    public List<long>? Ids { get; init; }
    public long? WarehouseId { get; init; }
    public List<ParcelStatus>? Statuses { get; init; }
    public bool? IsDeleted { get; init; }
}
