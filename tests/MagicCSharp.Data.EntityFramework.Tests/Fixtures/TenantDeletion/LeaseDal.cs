using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>
///     A lease on an apartment: the grandchild of a building, through the apartment it is for. Its key to the apartment
///     is <c>Restrict</c>, so the apartment's delete waits for it.
/// </summary>
[Table("leases")]
public class LeaseDal
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("apartment_id")]
    public long ApartmentId { get; set; }

    [ForeignKey(nameof(ApartmentId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ApartmentDal? Apartment { get; set; }
}
