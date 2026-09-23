using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Acme.Leasing.Domains.Leases.Models.Entities;
using MagicCSharp.Data.EntityFramework.Dals;

namespace Acme.Leasing.Data.EntityFramework.Dals;

[Table("properties")]
public class PropertyDal : BaseIdDal<Property, PropertyEdit>
{
    [Required]
    [Column("name")]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    [Column("address")]
    [StringLength(500)]
    public string Address { get; set; } = null!;

    [Required]
    [Column("time_zone_id")]
    [StringLength(100)]
    public string TimeZoneId { get; set; } = null!;

    [InverseProperty(nameof(LeaseDal.Property))]
    public virtual List<LeaseDal> Leases { get; set; } = [];

    [InverseProperty(nameof(LateFeePolicyDal.Property))]
    public virtual LateFeePolicyDal? LateFeePolicy { get; set; }

    /// <summary>Row to entity. Read every column the entity exposes.</summary>
    public override Property ToEntity()
    {
        return new Property
        {
            Id = Id,
            Name = Name,
            Address = Address,
            TimeZoneId = TimeZoneId,
            Created = Created,
            Updated = Updated,
        };
    }

    /// <summary>
    ///     Edit onto row. Assign every writable field unconditionally — the repository decides whether
    ///     anything changed by asking the change tracker afterwards.
    /// </summary>
    public override void Apply(PropertyEdit edit)
    {
        Name = edit.Name;
        Address = edit.Address;
        TimeZoneId = edit.TimeZoneId;
    }

    /// <summary>Build a new row. The id is assigned by the caller, before insert.</summary>
    public static PropertyDal From(PropertyEdit edit, long id)
    {
        var propertyDal = new PropertyDal
        {
            Id = id,
        };
        propertyDal.Apply(edit);
        return propertyDal;
    }
}
