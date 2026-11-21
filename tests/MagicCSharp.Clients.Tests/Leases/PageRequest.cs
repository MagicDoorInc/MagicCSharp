namespace MagicCSharp.Clients.Tests.Leases;

public record PageRequest
{
    public required int Page { get; init; }

    public required int PageSize { get; init; }
}
