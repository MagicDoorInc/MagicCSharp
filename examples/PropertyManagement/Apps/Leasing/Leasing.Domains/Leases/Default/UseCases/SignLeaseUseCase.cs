using Acme.Leasing.Domains.Charges.Models.Entities;
using Acme.Leasing.Domains.Charges.UseCases;
using Acme.Leasing.Domains.Leases.Models.Entities;
using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Libraries.Events.Leases;
using MagicCSharp.Events.Events;
using MagicCSharp.Infrastructure.Exceptions;
using MagicCSharp.UseCases;
using Microsoft.Extensions.Logging;

namespace Acme.Leasing.Domains.Leases.UseCases;

public interface ISignLeaseUseCase : IMagicUseCase
{
    Task<SignLeaseResult> Execute(SignLeaseRequest request);
}

public record SignLeaseRequest
{
    public required long PropertyId { get; init; }
    public required string TenantName { get; init; }
    public required string TenantEmail { get; init; }
    public required decimal MonthlyRent { get; init; }
    public required decimal SecurityDeposit { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
}

public record SignLeaseResult
{
    public required Lease Lease { get; init; }
    public required IReadOnlyList<Charge> Charges { get; init; }
}

/// <summary>
///     Signing is three operations that each exist on their own — find the property, create the lease, raise
///     what the tenant owes on day one — and one announcement. The welcome email is not a step here: it is a
///     handler on <see cref="LeaseSignedEvent" />, because a lease is signed whether or not the email goes.
/// </summary>
public class SignLeaseUseCase(
    IGetPropertiesUseCase getProperties,
    ICreateLeaseUseCase createLease,
    ICreateChargesUseCase createCharges,
    IEventDispatcher eventDispatcher,
    ILogger<SignLeaseUseCase> logger) : ISignLeaseUseCase
{
    public async Task<SignLeaseResult> Execute(SignLeaseRequest request)
    {
        logger.LogTrace("Executing: request={request}", request);

        var property = await getProperties.Execute(request.PropertyId);
        NotFoundException.ThrowIfNull(property, request.PropertyId);

        var lease = await createLease.Execute(new CreateLeaseRequest
        {
            PropertyId = property.Id,
            TenantName = request.TenantName,
            TenantEmail = request.TenantEmail,
            MonthlyRent = request.MonthlyRent,
            SecurityDeposit = request.SecurityDeposit,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
        });

        var charges = await createCharges.Execute(DayOneCharges(lease));

        eventDispatcher.Dispatch(new LeaseSignedEvent
        {
            LeaseId = lease.Id,
            PropertyId = property.Id,
        });

        return new SignLeaseResult
        {
            Lease = lease,
            Charges = charges,
        };
    }

    private static List<ChargeEdit> DayOneCharges(Lease lease)
    {
        var charges = new List<ChargeEdit>
        {
            new ChargeEdit
            {
                LeaseId = lease.Id,
                Type = ChargeType.Rent,
                Amount = lease.MonthlyRent,
                DueDate = lease.StartDate,
            },
        };

        if (lease.SecurityDeposit > 0)
        {
            charges.Add(new ChargeEdit
            {
                LeaseId = lease.Id,
                Type = ChargeType.SecurityDeposit,
                Amount = lease.SecurityDeposit,
                DueDate = lease.StartDate,
            });
        }

        return charges;
    }
}
