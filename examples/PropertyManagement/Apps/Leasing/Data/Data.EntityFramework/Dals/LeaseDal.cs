using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Acme.Leasing.Domains.Leases.Models.Entities;
using MagicCSharp.Data.EntityFramework.Dals;
using Microsoft.EntityFrameworkCore;

namespace Acme.Leasing.Data.EntityFramework.Dals;

[Table("leases")]
public class LeaseDal : BaseIdDal<Lease, LeaseEdit>
{
    /// <summary>A lease never moves to another property, so this is set once, in <see cref="From" />, and Apply leaves it alone.</summary>
    [Required]
    [Column("property_id")]
    public required long PropertyId { get; set; }

    [Required]
    [Column("tenant_name")]
    [StringLength(200)]
    public string TenantName { get; set; } = null!;

    [Required]
    [Column("tenant_email")]
    [StringLength(320)]
    public string TenantEmail { get; set; } = null!;

    [Required]
    [Column("monthly_rent")]
    public decimal MonthlyRent { get; set; }

    [Required]
    [Column("security_deposit")]
    public decimal SecurityDeposit { get; set; }

    [Required]
    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Required]
    [Column("end_date")]
    public DateOnly EndDate { get; set; }

    [ForeignKey(nameof(PropertyId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual PropertyDal Property { get; set; } = null!;

    [InverseProperty(nameof(ChargeDal.Lease))]
    public virtual List<ChargeDal> Charges { get; set; } = [];

    [InverseProperty(nameof(NotificationDal.Lease))]
    public virtual List<NotificationDal> Notifications { get; set; } = [];

    /// <summary>Row to entity. Read every column the entity exposes.</summary>
    public override Lease ToEntity()
    {
        return new Lease
        {
            Id = Id,
            PropertyId = PropertyId,
            TenantName = TenantName,
            TenantEmail = TenantEmail,
            MonthlyRent = MonthlyRent,
            SecurityDeposit = SecurityDeposit,
            StartDate = StartDate,
            EndDate = EndDate,
            Created = Created,
            Updated = Updated,
        };
    }

    /// <summary>
    ///     Edit onto row. Assign every writable field unconditionally — the repository decides whether
    ///     anything changed by asking the change tracker afterwards.
    /// </summary>
    public override void Apply(LeaseEdit edit)
    {
        TenantName = edit.TenantName;
        TenantEmail = edit.TenantEmail;
        MonthlyRent = edit.MonthlyRent;
        SecurityDeposit = edit.SecurityDeposit;
        StartDate = edit.StartDate;
        EndDate = edit.EndDate;
    }

    /// <summary>Build a new row. The id is assigned by the caller, before insert.</summary>
    public static LeaseDal From(LeaseEdit edit, long id)
    {
        var leaseDal = new LeaseDal
        {
            Id = id,
            PropertyId = edit.PropertyId,
        };
        leaseDal.Apply(edit);
        return leaseDal;
    }
}
