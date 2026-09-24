using System.ComponentModel.DataAnnotations;
using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;
using MagicCSharp.UseCases;
using Microsoft.Extensions.Logging;

namespace Acme.Leasing.Domains.Leases.UseCases;

public interface ICreateLeaseUseCase : IMagicUseCase
{
    Task<Lease> Execute(CreateLeaseRequest request);
}

public record CreateLeaseRequest
{
    public required long PropertyId { get; init; }
    public required string TenantName { get; init; }
    public required string TenantEmail { get; init; }
    public required decimal MonthlyRent { get; init; }
    public required decimal SecurityDeposit { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
}

public class CreateLeaseUseCase(
    ILeasesRepository leasesRepository,
    ILogger<CreateLeaseUseCase> logger) : ICreateLeaseUseCase
{
    public async Task<Lease> Execute(CreateLeaseRequest request)
    {
        logger.LogTrace("Executing: request={request}", request);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MonthlyRent, nameof(request.MonthlyRent));
        ArgumentOutOfRangeException.ThrowIfNegative(request.SecurityDeposit, nameof(request.SecurityDeposit));

        if (request.EndDate <= request.StartDate)
        {
            throw new ValidationException("A lease must end after it starts.");
        }

        return await leasesRepository.Create(new LeaseEdit
        {
            PropertyId = request.PropertyId,
            TenantName = request.TenantName,
            TenantEmail = request.TenantEmail,
            MonthlyRent = request.MonthlyRent,
            SecurityDeposit = request.SecurityDeposit,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
        });
    }
}
