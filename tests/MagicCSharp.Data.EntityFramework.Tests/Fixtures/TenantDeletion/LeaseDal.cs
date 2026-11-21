using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>A lease on an apartment: the grandchild of a building, through the apartment it is for.</summary>
[Table("leases")]
public class LeaseDal
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("apartment_id")]
    public long ApartmentId { get; set; }

    [ForeignKey(nameof(ApartmentId))]
    public ApartmentDal? Apartment { get; set; }
}
