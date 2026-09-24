using Acme.Leasing.Domains.Leases.Models.Entities;

namespace Acme.Leasing.Domains.Leases.App.Models;

public record PropertyDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Address { get; init; }
    public required string TimeZoneId { get; init; }
    public required DateTimeOffset Created { get; init; }

    public static PropertyDto FromEntity(Property property)
    {
        return new PropertyDto
        {
            Id = property.Id.ToString(),
            Name = property.Name,
            Address = property.Address,
            TimeZoneId = property.TimeZoneId,
            Created = property.Created,
        };
    }
}
