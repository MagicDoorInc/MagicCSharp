namespace MagicCSharp.Clients.Tests.Leases;

public record LeaseFilter
{
    public IReadOnlyList<long>? LeaseIds { get; init; }

    public string? Status { get; init; }
}
