using Acme.Leasing.Domains.Leases.Models.Entities;

namespace Acme.Leasing.Domains.Leases.App.Models;

public record LeaseDto
{
    public required string Id { get; init; }
    public required string PropertyId { get; init; }
    public required string TenantName { get; init; }
    public required string TenantEmail { get; init; }
    public required decimal MonthlyRent { get; init; }
    public required decimal SecurityDeposit { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required DateTimeOffset Created { get; init; }

    public static LeaseDto FromEntity(Lease lease)
    {
        return new LeaseDto
        {
            Id = lease.Id.ToString(),
            PropertyId = lease.PropertyId.ToString(),
            TenantName = lease.TenantName,
            TenantEmail = lease.TenantEmail,
            MonthlyRent = lease.MonthlyRent,
            SecurityDeposit = lease.SecurityDeposit,
            StartDate = lease.StartDate,
            EndDate = lease.EndDate,
            Created = lease.Created,
        };
    }
}
