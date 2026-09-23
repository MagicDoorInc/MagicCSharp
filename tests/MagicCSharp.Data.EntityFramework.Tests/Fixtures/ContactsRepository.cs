using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Infrastructure.KeyGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>
///     Search and pagination. Note what this cannot also be: soft delete lives on a sibling base class, so
///     one repository cannot inherit both. That is why the soft-delete tests use their own repository over
///     this same table.
/// </summary>
public class ContactsRepository(
    IDbContextFactory<TestDbContext> contextFactory,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
    : BaseIdSearchRepository<TestDbContext, ContactDal, Contact, ContactFilter, ContactEdit>(contextFactory, timeProvider, loggerFactory)
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
        if (!filter.ShouldIncludeDeleted)
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
