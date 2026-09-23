using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Charges.LateFees.Models.Entities;
using Acme.Leasing.Domains.Leases.UseCases;
using MagicCSharp.Infrastructure.Exceptions;
using MagicCSharp.UseCases;
using Microsoft.Extensions.Logging;

namespace Acme.Leasing.Domains.Charges.LateFees.UseCases;

public interface ISetLateFeePolicyUseCase : IMagicUseCase
{
    Task<LateFeePolicy> Execute(long propertyId, SetLateFeePolicyRequest request);
}

public record SetLateFeePolicyRequest
{
    public required int GraceDays { get; init; }
    public required decimal Amount { get; init; }
}

public class SetLateFeePolicyUseCase(
    IGetPropertiesUseCase getProperties,
    ILateFeePoliciesRepository lateFeePoliciesRepository,
    ILogger<SetLateFeePolicyUseCase> logger) : ISetLateFeePolicyUseCase
{
    public async Task<LateFeePolicy> Execute(long propertyId, SetLateFeePolicyRequest request)
    {
        logger.LogTrace("Executing: propertyId={propertyId}, request={request}", propertyId, request);

        ArgumentOutOfRangeException.ThrowIfNegative(request.GraceDays, nameof(request.GraceDays));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Amount, nameof(request.Amount));

        var property = await getProperties.Execute(propertyId);
        NotFoundException.ThrowIfNull(property, propertyId);

        var lateFeePolicyEdit = new LateFeePolicyEdit
        {
            PropertyId = property.Id,
            GraceDays = request.GraceDays,
            Amount = request.Amount,
        };

        var existingLateFeePolicy = await lateFeePoliciesRepository.Get(property.Id);
        if (existingLateFeePolicy == null)
        {
            return await lateFeePoliciesRepository.Create(lateFeePolicyEdit);
        }

        return await lateFeePoliciesRepository.Update(property.Id, lateFeePolicyEdit);
    }
}
