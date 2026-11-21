using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>The other side of the pair.</summary>
[Table("right_halves")]
public class RightHalfDal
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("left_half_id")]
    public long? LeftHalfId { get; set; }

    [ForeignKey(nameof(LeftHalfId))]
    public LeftHalfDal? LeftHalf { get; set; }
}
