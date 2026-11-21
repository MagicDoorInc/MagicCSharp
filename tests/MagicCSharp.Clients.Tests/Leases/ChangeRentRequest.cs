namespace MagicCSharp.Clients.Tests.Leases;

public record ChangeRentRequest
{
    public required decimal MonthlyRent { get; init; }
}
