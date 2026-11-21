using System.ComponentModel.DataAnnotations.Schema;
using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>
///     An apartment in a building. Its key to the building is <c>Restrict</c>: the database refuses to delete a building
///     that still has apartments, so tenant deletion must empty this table first.
/// </summary>
public record Apartment : ApartmentEdit, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

public record ApartmentEdit
{
    public required long TenantId { get; init; }
    public required long BuildingId { get; init; }
    public required string Number { get; init; }
}

[Table("apartments")]
public partial class ApartmentDal : BaseIdDal<Apartment, ApartmentEdit>
{
    [Column("tenant_id")]
    public long TenantId { get; set; }

    [Column("building_id")]
    public long BuildingId { get; set; }

    [Column("number")]
    public string Number { get; set; } = "";

    [ForeignKey(nameof(BuildingId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public BuildingDal? Building { get; set; }
}
