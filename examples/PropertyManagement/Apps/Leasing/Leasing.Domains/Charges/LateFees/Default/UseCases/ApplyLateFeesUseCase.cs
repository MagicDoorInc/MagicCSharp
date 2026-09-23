using Acme.Leasing.Domains.Charges.LateFees.Models.Entities;
using Acme.Leasing.Domains.Charges.Models.Entities;
using Acme.Leasing.Domains.Charges.UseCases;
using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Libraries.Events.Charges;
using MagicCSharp.Events.Events;
using MagicCSharp.Infrastructure;
using MagicCSharp.UseCases;
using Medallion.Threading;
using Microsoft.Extensions.Logging;

namespace Acme.Leasing.Domains.Charges.LateFees.UseCases;

public interface IApplyLateFeesUseCase : IMagicUseCase
{
    Task<ApplyLateFeesResult> Execute();
}

public record ApplyLateFeesResult
{
    public required int LateFeesApplied { get; init; }
}

/// <summary>
///     Charges a late fee on every rent charge that is unpaid past its property's grace period, once.
///     <para>
///         Safe to run as often as you like: a rent charge that already has a late fee is skipped, so the
///         hourly job and a manual trigger can both call this without charging twice.
///     </para>
/// </summary>
public class ApplyLateFeesUseCase(
    IGetChargesUseCase getCharges,
    ICreateChargesUseCase createCharges,
    IGetLeasesUseCase getLeases,
    IGetPropertiesUseCase getProperties,
    IGetLateFeePoliciesUseCase getLateFeePolicies,
    TimeProvider timeProvider,
    IEventDispatcher eventDispatcher,
    IDistributedLockProvider distributedLockProvider,
    ILogger<ApplyLateFeesUseCase> logger) : IApplyLateFeesUseCase
{
    public async Task<ApplyLateFeesResult> Execute()
    {
        logger.LogTrace("Executing: apply late fees");

        // "Already has a late fee" is only true once the fee is written. Two runs overlapping would both
        // see it missing.
        await using var lateFeesLock = await distributedLockProvider.AcquireLockAsync("apply-late-fees");

        var now = timeProvider.GetUtcNow();

        // Late means the property's local date is past the due date, and no zone is more than a day ahead
        // of UTC — so nothing due after today's UTC date can be late anywhere yet.
        var unpaidRentCharges = await getCharges.Execute(new ChargeFilter
        {
            Types = [ChargeType.Rent],
            IsPaid = false,
            DueDate = new ComparableRange<DateOnly>
            {
                End = DateOnly.FromDateTime(now.UtcDateTime),
            },
        });

        if (unpaidRentCharges.Count == 0)
        {
            return new ApplyLateFeesResult
            {
                LateFeesApplied = 0,
            };
        }

        var existingLateFees = await getCharges.Execute(new ChargeFilter
        {
            LateFeeForChargeIds = unpaidRentCharges.Select(x => x.Id).ToList(),
        });
        var rentChargeIdsWithLateFee = existingLateFees
            .Where(x => x.LateFeeForChargeId != null)
            .Select(x => x.LateFeeForChargeId!.Value)
            .ToHashSet();

        var leaseIds = unpaidRentCharges.Select(x => x.LeaseId).Distinct().ToList();
        var leases = await getLeases.Execute(leaseIds);
        var leaseById = leases.ToDictionary(x => x.Id);

        var propertyIds = leases.Select(x => x.PropertyId).Distinct().ToList();
        var properties = await getProperties.Execute(propertyIds);
        var propertyById = properties.ToDictionary(x => x.Id);

        var lateFeePolicies = await getLateFeePolicies.Execute(propertyIds);
        var lateFeePolicyByPropertyId = lateFeePolicies.ToDictionary(x => x.PropertyId);

        var lateFeeEdits = new List<ChargeEdit>();
        foreach (var rentCharge in unpaidRentCharges)
        {
            if (rentChargeIdsWithLateFee.Contains(rentCharge.Id))
            {
                continue;
            }

            var lease = leaseById[rentCharge.LeaseId];
            var property = propertyById[lease.PropertyId];

            // A property without a policy does not charge late fees.
            if (!lateFeePolicyByPropertyId.TryGetValue(property.Id, out var lateFeePolicy))
            {
                continue;
            }

            var today = property.LocalDate(now);
            if (!IsLate(rentCharge, lateFeePolicy, today))
            {
                continue;
            }

            lateFeeEdits.Add(LateFeeFor(rentCharge, lateFeePolicy, today));
        }

        if (lateFeeEdits.Count == 0)
        {
            return new ApplyLateFeesResult
            {
                LateFeesApplied = 0,
            };
        }

        var lateFees = await createCharges.Execute(lateFeeEdits);

        foreach (var lateFeeCharge in lateFees)
        {
            eventDispatcher.Dispatch(new LateFeeAppliedEvent
            {
                LateFeeChargeId = lateFeeCharge.Id,
                RentChargeId = lateFeeCharge.LateFeeForChargeId!.Value,
                LeaseId = lateFeeCharge.LeaseId,
            });
        }

        return new ApplyLateFeesResult
        {
            LateFeesApplied = lateFees.Count,
        };
    }

    private static bool IsLate(Charge rentCharge, LateFeePolicy lateFeePolicy, DateOnly today)
    {
        var lastDayOnTime = rentCharge.DueDate.AddDays(lateFeePolicy.GraceDays);
        return today > lastDayOnTime;
    }

    private static ChargeEdit LateFeeFor(Charge rentCharge, LateFeePolicy lateFeePolicy, DateOnly today)
    {
        return new ChargeEdit
        {
            LeaseId = rentCharge.LeaseId,
            Type = ChargeType.LateFee,
            Amount = lateFeePolicy.Amount,
            DueDate = today,
            LateFeeForChargeId = rentCharge.Id,
        };
    }
}
