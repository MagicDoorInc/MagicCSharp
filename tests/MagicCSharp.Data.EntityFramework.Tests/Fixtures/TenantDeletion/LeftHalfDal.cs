using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>One side of a pair of tables whose <c>Restrict</c> keys point at each other.</summary>
[Table("left_halves")]
public class LeftHalfDal
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("right_half_id")]
    public long? RightHalfId { get; set; }

    [ForeignKey(nameof(RightHalfId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public RightHalfDal? RightHalf { get; set; }
}
