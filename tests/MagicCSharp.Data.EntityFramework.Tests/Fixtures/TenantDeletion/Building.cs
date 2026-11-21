using System.ComponentModel.DataAnnotations.Schema;
using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>A tenant's building: the parent its apartments' Restrict key points at.</summary>
public record Building : BuildingEdit, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

public record BuildingEdit
{
    public required long TenantId { get; init; }
    public required string Name { get; init; }
}

[Table("buildings")]
public partial class BuildingDal : BaseIdDal<Building, BuildingEdit>
{
    [Column("tenant_id")]
    public long TenantId { get; set; }

    [Column("name")]
    public string Name { get; set; } = "";
}
