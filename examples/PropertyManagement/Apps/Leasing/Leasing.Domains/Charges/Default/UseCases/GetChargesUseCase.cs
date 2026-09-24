using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Charges.Models.Entities;
using MagicCSharp.Data.Models;
using MagicCSharp.UseCases;

namespace Acme.Leasing.Domains.Charges.UseCases;

public interface IGetChargesUseCase : IMagicUseCase
{
    Task<Charge?> Execute(long chargeId);
    Task<IReadOnlyList<Charge>> Execute(ChargeFilter filter);
    Task<Pagination<Charge>> Execute(PaginationRequest paginationRequest, ChargeFilter filter);
}

public class GetChargesUseCase(IChargesRepository chargesRepository) : IGetChargesUseCase
{
    public async Task<Charge?> Execute(long chargeId)
    {
        return await chargesRepository.Get(chargeId);
    }

    public async Task<IReadOnlyList<Charge>> Execute(ChargeFilter filter)
    {
        return await chargesRepository.Get(filter);
    }

    public async Task<Pagination<Charge>> Execute(PaginationRequest paginationRequest, ChargeFilter filter)
    {
        return await chargesRepository.Get(paginationRequest, filter);
    }
}
