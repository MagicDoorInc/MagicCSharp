using MagicCSharp.Data.EntityFramework.Repositories;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>A second repository over the apartments table, as a read model beside the main one would be.</summary>
public class ApartmentArchiveRepository(RepositoryServices<TenantDeletionDbContext> repositoryServices)
    : BaseIdRepository<TenantDeletionDbContext, ApartmentDal, Apartment, TenantRowFilter, ApartmentEdit>(repositoryServices)
{
    protected override IQueryable<ApartmentDal> ApplyFilter(IQueryable<ApartmentDal> query, TenantRowFilter filter)
    {
        return query;
    }
}
