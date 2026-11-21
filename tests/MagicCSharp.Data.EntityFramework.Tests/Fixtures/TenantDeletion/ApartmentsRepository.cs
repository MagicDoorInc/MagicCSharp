using MagicCSharp.Data.EntityFramework.Repositories;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

public class ApartmentsRepository(RepositoryServices<TenantDeletionDbContext> repositoryServices)
    : BaseIdRepository<TenantDeletionDbContext, ApartmentDal, Apartment, TenantRowFilter, ApartmentEdit>(repositoryServices)
{
    protected override IQueryable<ApartmentDal> ApplyFilter(IQueryable<ApartmentDal> query, TenantRowFilter filter)
    {
        return query;
    }
}
