using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Infrastructure.KeyGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>The same table through the soft-delete base, since one class cannot have both.</summary>
public class ContactsSoftDeleteRepository(
    IDbContextFactory<TestDbContext> contextFactory,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
    : BaseIdSoftDeleteRepository<TestDbContext, ContactDal, Contact, ContactFilter, ContactEdit>(contextFactory, timeProvider, loggerFactory)
{
    public required IKeyGenService Ids { get; init; }

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

        return filter.City != null ? query.Where(contact => contact.City == filter.City) : query;
    }
}
