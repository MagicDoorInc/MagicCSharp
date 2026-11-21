namespace MagicCSharp.Clients.Tests.Leases;

public record CreateLeaseRequest
{
    public required string TenantName { get; init; }

    public required decimal MonthlyRent { get; init; }
}
