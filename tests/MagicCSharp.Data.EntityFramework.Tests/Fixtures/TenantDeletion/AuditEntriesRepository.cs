using MagicCSharp.Data.EntityFramework.Repositories;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

public class AuditEntriesRepository(RepositoryServices<TenantDeletionDbContext> repositoryServices)
    : BaseIdRepository<TenantDeletionDbContext, AuditEntryDal, AuditEntry, TenantRowFilter, AuditEntryEdit>(repositoryServices)
{
    protected override IQueryable<AuditEntryDal> ApplyFilter(IQueryable<AuditEntryDal> query, TenantRowFilter filter)
    {
        return query;
    }
}
