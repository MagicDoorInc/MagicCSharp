using System.ComponentModel.DataAnnotations.Schema;
using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>What happened in a tenant's account; kept when the tenant is deleted.</summary>
public record AuditEntry : AuditEntryEdit, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

public record AuditEntryEdit
{
    public required long TenantId { get; init; }
    public required string Text { get; init; }
}

[Table("audit_entries")]
[KeptOnTenantDeletion("The audit trail outlives the tenant it records")]
public partial class AuditEntryDal : BaseIdDal<AuditEntry, AuditEntryEdit>
{
    [Column("tenant_id")]
    public long TenantId { get; set; }

    [Column("text")]
    public string Text { get; set; } = "";
}
