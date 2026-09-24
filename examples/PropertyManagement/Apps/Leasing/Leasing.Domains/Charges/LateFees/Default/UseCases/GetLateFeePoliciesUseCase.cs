using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Charges.LateFees.Models.Entities;
using MagicCSharp.UseCases;

namespace Acme.Leasing.Domains.Charges.LateFees.UseCases;

public interface IGetLateFeePoliciesUseCase : IMagicUseCase
{
    Task<LateFeePolicy?> Execute(long propertyId);
    Task<IReadOnlyList<LateFeePolicy>> Execute(IReadOnlyList<long> propertyIds);
}

public class GetLateFeePoliciesUseCase(ILateFeePoliciesRepository lateFeePoliciesRepository)
    : IGetLateFeePoliciesUseCase
{
    public async Task<LateFeePolicy?> Execute(long propertyId)
    {
        return await lateFeePoliciesRepository.Get(propertyId);
    }

    public async Task<IReadOnlyList<LateFeePolicy>> Execute(IReadOnlyList<long> propertyIds)
    {
        return await lateFeePoliciesRepository.Get(propertyIds);
    }
}
