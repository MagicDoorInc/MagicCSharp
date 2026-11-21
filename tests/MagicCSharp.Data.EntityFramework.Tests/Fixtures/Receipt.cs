using System.ComponentModel.DataAnnotations.Schema;
using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>A tenant's receipt, keyed by an unguessable string key.</summary>
public record Receipt : ReceiptEdit, IKeyEntity
{
    public required string Key { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

public record ReceiptEdit
{
    public required long TenantId { get; init; }
    public required string Text { get; init; }
}

public class ReceiptFilter
{
    public long? TenantId { get; init; }
}

[Table("receipts")]
public class ReceiptDal : BaseKeyDal<Receipt, ReceiptEdit>
{
    [Column("tenant_id")]
    public long TenantId { get; set; }

    [Column("text")]
    public string Text { get; set; } = "";

    public override Receipt ToEntity()
    {
        return new Receipt { Key = Key, Created = Created, Updated = Updated, TenantId = TenantId, Text = Text };
    }

    public override void Apply(ReceiptEdit edit)
    {
        Text = edit.Text;
    }
}
