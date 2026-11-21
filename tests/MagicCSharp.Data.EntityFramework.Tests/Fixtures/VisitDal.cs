using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>A row whose owner column is nullable: a visit nobody has claimed has no owner.</summary>
[Table("visits")]
public class VisitDal
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("id")]
    public long Id { get; set; }

    [Column("owner_id")]
    public long? OwnerId { get; set; }
}
