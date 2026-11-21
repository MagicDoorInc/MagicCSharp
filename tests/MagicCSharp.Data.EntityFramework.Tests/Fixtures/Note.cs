using System.ComponentModel.DataAnnotations.Schema;
using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>A tenant's note: the entity whose Dal maps itself, through MagicCSharp.Generators.</summary>
public record Note : NoteEdit, IIdEntity, IDeletedEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
    public DateTimeOffset? Deleted { get; init; }
}

public record NoteEdit
{
    public required long TenantId { get; init; }
    public required string Text { get; init; }
    public DateTimeOffset? Pinned { get; init; }
}

public class NoteFilter
{
    public long? TenantId { get; init; }
}

/// <summary>
///     Only columns: <c>partial</c> is what makes the generator write <c>ToEntity</c>, <c>Apply</c> and <c>From</c>.
///     <c>TenantId</c> is <c>required</c>, so <c>From</c> sets it and <c>Apply</c> never moves a note to another tenant.
/// </summary>
[Table("notes")]
public partial class NoteDal : BaseIdDal<Note, NoteEdit>, IDalDeleted
{
    [Column("tenant_id")]
    public required long TenantId { get; set; }

    [Column("text")]
    public string Text { get; set; } = "";

    [Column("pinned")]
    public DateTimeOffset? Pinned { get; set; }

    [Column("deleted")]
    public DateTimeOffset? Deleted { get; set; }
}
