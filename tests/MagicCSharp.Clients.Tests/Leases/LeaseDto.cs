namespace MagicCSharp.Clients.Tests.Leases;

public record LeaseDto
{
    public required long Id { get; init; }

    public required string TenantName { get; init; }
}
