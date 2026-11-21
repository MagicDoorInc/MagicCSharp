using MagicCSharp.Data.EntityFramework.Repositories;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

public class ApartmentPhotosRepository(RepositoryServices<TenantDeletionDbContext> repositoryServices)
    : BaseIdRepository<TenantDeletionDbContext, ApartmentPhotoDal, ApartmentPhoto, TenantRowFilter, ApartmentPhotoEdit>(repositoryServices)
{
    protected override IQueryable<ApartmentPhotoDal> ApplyFilter(IQueryable<ApartmentPhotoDal> query, TenantRowFilter filter)
    {
        return query;
    }
}
