using System.ComponentModel.DataAnnotations.Schema;
using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>A tenant's shipment: the entity whose filter is queried and matched by generated code alone.</summary>
public record Shipment : ShipmentEdit, IIdEntity, IDeletedEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
    public DateTimeOffset? Deleted { get; init; }
}

public record ShipmentEdit
{
    public required long TenantId { get; init; }
    public required ShipmentStatus Status { get; init; }
    public long? CarrierId { get; init; }
    public required string TrackingCode { get; init; }
    public DateTimeOffset? Delivered { get; init; }
}

public enum ShipmentStatus
{
    Booked,
    InTransit,
    Delivered,
}

/// <summary>
///     Every rule the generators know, over every column kind, plus one custom field. <c>[GenerateMatches]</c> is what
///     makes the generator write <c>Matches</c>; <c>IEntityFilter</c> names the entity it tests.
/// </summary>
[GenerateMatches]
public partial class ShipmentFilter : IEntityFilter<Shipment>
{
    public List<long>? Ids { get; init; }
    public long? TenantId { get; init; }
    public List<ShipmentStatus>? Statuses { get; init; }
    public List<long>? CarrierIds { get; init; }
    public long? CarrierId { get; init; }
    public List<string>? TrackingCodes { get; init; }
    public bool? IsDelivered { get; init; }
    public bool? IsDeleted { get; init; }
    public ComparableRange<DateTimeOffset>? Created { get; init; }
    public ComparableRange<DateTimeOffset>? DeliveredRange { get; init; }
    public string? TrackingCodeStartsWith { get; init; }

    private partial bool MatchesCustomFilter(Shipment entity)
    {
        return TrackingCodeStartsWith == null || entity.TrackingCode.StartsWith(TrackingCodeStartsWith, StringComparison.Ordinal);
    }
}

[Table("shipments")]
[GenerateDalMapping]
public partial class ShipmentDal : BaseIdDal<Shipment, ShipmentEdit>, IDalDeleted
{
    [Column("tenant_id")]
    public required long TenantId { get; set; }

    [Column("status")]
    public ShipmentStatus Status { get; set; }

    [Column("carrier_id")]
    public long? CarrierId { get; set; }

    [Column("tracking_code")]
    public string TrackingCode { get; set; } = "";

    [Column("delivered")]
    public DateTimeOffset? Delivered { get; set; }

    [Column("deleted")]
    public DateTimeOffset? Deleted { get; set; }
}
