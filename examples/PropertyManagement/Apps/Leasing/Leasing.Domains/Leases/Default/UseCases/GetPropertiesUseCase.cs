using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;
using MagicCSharp.Data.Models;
using MagicCSharp.UseCases;

namespace Acme.Leasing.Domains.Leases.UseCases;

public interface IGetPropertiesUseCase : IMagicUseCase
{
    Task<Property?> Execute(long propertyId);
    Task<IReadOnlyList<Property>> Execute(IReadOnlyList<long> propertyIds);
    Task<Pagination<Property>> Execute(PaginationRequest paginationRequest, PropertyFilter filter);
}

public class GetPropertiesUseCase(IPropertiesRepository propertiesRepository) : IGetPropertiesUseCase
{
    public async Task<Property?> Execute(long propertyId)
    {
        return await propertiesRepository.Get(propertyId);
    }

    public async Task<IReadOnlyList<Property>> Execute(IReadOnlyList<long> propertyIds)
    {
        return await propertiesRepository.Get(propertyIds);
    }

    public async Task<Pagination<Property>> Execute(PaginationRequest paginationRequest, PropertyFilter filter)
    {
        return await propertiesRepository.Get(paginationRequest, filter);
    }
}
