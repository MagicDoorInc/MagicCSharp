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
    public bool IncludeDeleted { get; init; }
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

/// <summary>
///     Search and pagination. Note what this cannot also be: soft delete lives on a sibling base class, so
///     one repository cannot inherit both. That is why the soft-delete tests use their own repository over
///     this same table.
/// </summary>
public class ContactsRepository(
    IDbContextFactory<TestDbContext> contextFactory,
    IClock clock,
    ILoggerFactory loggerFactory)
    : BaseIdSearchRepository<TestDbContext, ContactDal, Contact, ContactFilter, ContactEdit>(contextFactory, clock, loggerFactory)
{
    public required IKeyGenService Ids { get; init; }

    /// <summary>Both sides of a search go through the same map, which is what makes "st" find "street".</summary>
    protected override IReadOnlyDictionary<string, string>? SearchSynonyms =>
        new Dictionary<string, string> { ["street"] = "st" };

    protected override ContactDal CreateDal(ContactEdit edit)
    {
        return new ContactDal { Id = Ids.GetId(), Name = edit.Name, City = edit.City };
    }

    protected override IQueryable<ContactDal> ApplyFilter(IQueryable<ContactDal> query, ContactFilter filter)
    {
        if (!filter.IncludeDeleted)
        {
            query = query.Where(contact => contact.Deleted == null);
        }

        if (filter.City != null)
        {
            query = query.Where(contact => contact.City == filter.City);
        }

        return ApplySearch(query, filter.Search);
    }

    /// <summary>Exposed so a test can drive the search column the way a use case would.</summary>
    public Task SetKeywords(long id, params string[] keywords)
    {
        return UpdateSearch(id, keywords);
    }
}

/// <summary>The same table through the soft-delete base, since one class cannot have both.</summary>
public class ContactsSoftDeleteRepository(
    IDbContextFactory<TestDbContext> contextFactory,
    IClock clock,
    ILoggerFactory loggerFactory)
    : BaseIdSoftDeleteRepository<TestDbContext, ContactDal, Contact, ContactFilter, ContactEdit>(contextFactory, clock, loggerFactory)
{
    public required IKeyGenService Ids { get; init; }

    protected override ContactDal CreateDal(ContactEdit edit)
    {
        return new ContactDal { Id = Ids.GetId(), Name = edit.Name, City = edit.City };
    }

    protected override IQueryable<ContactDal> ApplyFilter(IQueryable<ContactDal> query, ContactFilter filter)
    {
        if (!filter.IncludeDeleted)
        {
            query = query.Where(contact => contact.Deleted == null);
        }

        return filter.City != null ? query.Where(contact => contact.City == filter.City) : query;
    }
}
