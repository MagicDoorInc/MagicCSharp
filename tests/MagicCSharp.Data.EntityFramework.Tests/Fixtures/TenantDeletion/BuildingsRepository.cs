using MagicCSharp.Data.EntityFramework.Repositories;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

public class BuildingsRepository(RepositoryServices<TenantDeletionDbContext> repositoryServices)
    : BaseIdRepository<TenantDeletionDbContext, BuildingDal, Building, TenantRowFilter, BuildingEdit>(repositoryServices)
{
    protected override IQueryable<BuildingDal> ApplyFilter(IQueryable<BuildingDal> query, TenantRowFilter filter)
    {
        return query;
    }
}
