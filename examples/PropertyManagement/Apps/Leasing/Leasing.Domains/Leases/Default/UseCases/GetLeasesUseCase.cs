using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;
using MagicCSharp.Data.Models;
using MagicCSharp.UseCases;

namespace Acme.Leasing.Domains.Leases.UseCases;

public interface IGetLeasesUseCase : IMagicUseCase
{
    Task<Lease?> Execute(long leaseId);
    Task<IReadOnlyList<Lease>> Execute(IReadOnlyList<long> leaseIds);
    Task<Pagination<Lease>> Execute(PaginationRequest paginationRequest, LeaseFilter filter);
}

public class GetLeasesUseCase(ILeasesRepository leasesRepository) : IGetLeasesUseCase
{
    public async Task<Lease?> Execute(long leaseId)
    {
        return await leasesRepository.Get(leaseId);
    }

    public async Task<IReadOnlyList<Lease>> Execute(IReadOnlyList<long> leaseIds)
    {
        return await leasesRepository.Get(leaseIds);
    }

    public async Task<Pagination<Lease>> Execute(PaginationRequest paginationRequest, LeaseFilter filter)
    {
        return await leasesRepository.Get(paginationRequest, filter);
    }
}
