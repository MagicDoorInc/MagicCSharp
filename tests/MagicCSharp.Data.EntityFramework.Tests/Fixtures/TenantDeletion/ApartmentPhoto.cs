using System.ComponentModel.DataAnnotations.Schema;
using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>
///     A photo of an apartment. Its key to the apartment is <c>Cascade</c>: the database deletes the photos with their
///     apartment, so tenant deletion needs no order for this table.
/// </summary>
public record ApartmentPhoto : ApartmentPhotoEdit, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

public record ApartmentPhotoEdit
{
    public required long TenantId { get; init; }
    public required long ApartmentId { get; init; }
    public required string FileName { get; init; }
}

[Table("apartment_photos")]
public partial class ApartmentPhotoDal : BaseIdDal<ApartmentPhoto, ApartmentPhotoEdit>
{
    [Column("tenant_id")]
    public long TenantId { get; set; }

    [Column("apartment_id")]
    public long ApartmentId { get; set; }

    [Column("file_name")]
    public string FileName { get; set; } = "";

    [ForeignKey(nameof(ApartmentId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public ApartmentDal? Apartment { get; set; }
}
