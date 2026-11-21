using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.Boilers;

/// <summary>A boiler; its serial number came in the second migration.</summary>
[Table("boilers")]
public class BoilerDal
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("tenant_id")]
    public long TenantId { get; set; }

    [Column("serial_number")]
    public string? SerialNumber { get; set; }
}
