using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Acme.Leasing.Domains.Charges.Models.Entities;
using MagicCSharp.Data.EntityFramework.Dals;
using Microsoft.EntityFrameworkCore;

namespace Acme.Leasing.Data.EntityFramework.Dals;

[Table("charges")]
public class ChargeDal : BaseIdDal<Charge, ChargeEdit>
{
    /// <summary>A charge never moves to another lease or changes kind, so both are set once, in <see cref="From" />.</summary>
    [Required]
    [Column("lease_id")]
    public required long LeaseId { get; set; }

    [Required]
    [Column("type")]
    public required ChargeType Type { get; set; }

    [Required]
    [Column("amount")]
    public decimal Amount { get; set; }

    [Required]
    [Column("due_date")]
    public DateOnly DueDate { get; set; }

    [Column("paid")]
    public DateTimeOffset? Paid { get; set; }

    [Column("late_fee_for_charge_id")]
    public long? LateFeeForChargeId { get; set; }

    [ForeignKey(nameof(LeaseId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual LeaseDal Lease { get; set; } = null!;

    [ForeignKey(nameof(LateFeeForChargeId))]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public virtual ChargeDal? LateFeeForCharge { get; set; }

    /// <summary>Row to entity. Read every column the entity exposes.</summary>
    public override Charge ToEntity()
    {
        return new Charge
        {
            Id = Id,
            LeaseId = LeaseId,
            Type = Type,
            Amount = Amount,
            DueDate = DueDate,
            Paid = Paid,
            LateFeeForChargeId = LateFeeForChargeId,
            Created = Created,
            Updated = Updated,
        };
    }

    /// <summary>
    ///     Edit onto row. Assign every writable field unconditionally — the repository decides whether
    ///     anything changed by asking the change tracker afterwards.
    /// </summary>
    public override void Apply(ChargeEdit edit)
    {
        Amount = edit.Amount;
        DueDate = edit.DueDate;
        Paid = edit.Paid;
        LateFeeForChargeId = edit.LateFeeForChargeId;
    }

    /// <summary>Build a new row. The id is assigned by the caller, before insert.</summary>
    public static ChargeDal From(ChargeEdit edit, long id)
    {
        var chargeDal = new ChargeDal
        {
            Id = id,
            LeaseId = edit.LeaseId,
            Type = edit.Type,
        };
        chargeDal.Apply(edit);
        return chargeDal;
    }
}
