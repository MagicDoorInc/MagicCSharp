using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>One side of a pair of tables whose keys point at each other.</summary>
[Table("left_halves")]
public class LeftHalfDal
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("right_half_id")]
    public long? RightHalfId { get; set; }

    [ForeignKey(nameof(RightHalfId))]
    public RightHalfDal? RightHalf { get; set; }
}
