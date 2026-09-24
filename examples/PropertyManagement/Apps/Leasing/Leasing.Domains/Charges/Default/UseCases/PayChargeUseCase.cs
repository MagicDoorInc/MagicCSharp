using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Charges.Models.Entities;
using Acme.Libraries.Events.Charges;
using MagicCSharp.Events.Events;
using MagicCSharp.Infrastructure.Exceptions;
using MagicCSharp.UseCases;
using Medallion.Threading;
using Microsoft.Extensions.Logging;

namespace Acme.Leasing.Domains.Charges.UseCases;

public interface IPayChargeUseCase : IMagicUseCase
{
    Task<Charge> Execute(long chargeId);
}

public class PayChargeUseCase(
    IChargesRepository chargesRepository,
    TimeProvider timeProvider,
    IEventDispatcher eventDispatcher,
    IDistributedLockProvider distributedLockProvider,
    ILogger<PayChargeUseCase> logger) : IPayChargeUseCase
{
    public async Task<Charge> Execute(long chargeId)
    {
        logger.LogTrace("Executing: chargeId={chargeId}", chargeId);

        // Two payments for one charge arriving together would otherwise both see it unpaid, and the
        // tenant would get two receipts for one debt.
        await using var chargeLock = await distributedLockProvider.AcquireLockAsync($"charge-{chargeId}");

        var charge = await chargesRepository.Get(chargeId);
        NotFoundException.ThrowIfNull(charge, chargeId);

        if (charge.Paid != null)
        {
            throw new EntityInvalidOperationException($"Charge {chargeId} is already paid.");
        }

        var paidCharge = await chargesRepository.Update(charge with { Paid = timeProvider.GetUtcNow() });

        eventDispatcher.Dispatch(new ChargePaidEvent
        {
            ChargeId = paidCharge.Id,
            LeaseId = paidCharge.LeaseId,
        });

        return paidCharge;
    }
}
