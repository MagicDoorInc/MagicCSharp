using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>A notice on a tenant's board: no key to or from any other table.</summary>
[Table("notices")]
public class NoticeDal
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("tenant_id")]
    public long TenantId { get; set; }
}
