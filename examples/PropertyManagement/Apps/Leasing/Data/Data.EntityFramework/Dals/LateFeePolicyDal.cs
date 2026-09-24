using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Acme.Leasing.Domains.Charges.LateFees.Models.Entities;
using MagicCSharp.Data.EntityFramework.Dals;
using Microsoft.EntityFrameworkCore;

namespace Acme.Leasing.Data.EntityFramework.Dals;

[Table("late_fee_policies")]
public class LateFeePolicyDal : BaseIdDal<LateFeePolicy, LateFeePolicyEdit>
{
    [Required]
    [Column("grace_days")]
    public int GraceDays { get; set; }

    [Required]
    [Column("amount")]
    public decimal Amount { get; set; }

    /// <summary>The policy's id is its property's id, so the key doubles as the foreign key.</summary>
    [ForeignKey(nameof(Id))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual PropertyDal Property { get; set; } = null!;

    /// <summary>Row to entity. The id is the property's id, so it is read back as both.</summary>
    public override LateFeePolicy ToEntity()
    {
        return new LateFeePolicy
        {
            Id = Id,
            PropertyId = Id,
            GraceDays = GraceDays,
            Amount = Amount,
            Created = Created,
            Updated = Updated,
        };
    }

    /// <summary>
    ///     Edit onto row. Assign every writable field unconditionally — the repository decides whether
    ///     anything changed by asking the change tracker afterwards.
    /// </summary>
    public override void Apply(LateFeePolicyEdit edit)
    {
        GraceDays = edit.GraceDays;
        Amount = edit.Amount;
    }

    /// <summary>Build a new row, keyed by the property it belongs to.</summary>
    public static LateFeePolicyDal From(LateFeePolicyEdit edit)
    {
        var lateFeePolicyDal = new LateFeePolicyDal
        {
            Id = edit.PropertyId,
        };
        lateFeePolicyDal.Apply(edit);
        return lateFeePolicyDal;
    }
}
