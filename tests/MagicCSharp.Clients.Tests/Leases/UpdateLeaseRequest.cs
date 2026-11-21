namespace MagicCSharp.Clients.Tests.Leases;

public record UpdateLeaseRequest
{
    public required string TenantName { get; init; }
}
