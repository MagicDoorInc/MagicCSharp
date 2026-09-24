using System.ComponentModel.DataAnnotations.Schema;
using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Models;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;
using MagicCSharp.Infrastructure.KeyGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>An entity that opts into soft delete and the denormalized search column.</summary>
public record Contact : ContactEdit, IIdEntity, IDeletedEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
    public DateTimeOffset? Deleted { get; init; }
}

public record ContactEdit
{
    public required string Name { get; init; }
    public required string City { get; init; }
}

public class ContactFilter
{
    public string? Search { get; init; }
    public string? City { get; init; }
    public bool ShouldIncludeDeleted { get; init; }
}

[Table("contacts")]
public class ContactDal : BaseIdDal<Contact, ContactEdit>, IDalDeleted, IDalSearchField
{
    [Column("name")]
    public required string Name { get; set; }

    [Column("city")]
    public required string City { get; set; }

    [Column("deleted")]
    public DateTimeOffset? Deleted { get; set; }

    [Column("meta_data_search")]
    public string? MetaDataSearch { get; set; }

    public override Contact ToEntity()
    {
        return new Contact
        {
            Id = Id,
            Created = Created,
            Updated = Updated,
            Deleted = Deleted,
            Name = Name,
            City = City,
        };
    }

    public override void Apply(ContactEdit edit)
    {
        Name = edit.Name;
        City = edit.City;
    }
}
