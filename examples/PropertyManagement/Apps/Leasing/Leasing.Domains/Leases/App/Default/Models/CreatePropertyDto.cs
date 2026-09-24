using System.ComponentModel.DataAnnotations;
using Acme.Leasing.Domains.Leases.UseCases;

namespace Acme.Leasing.Domains.Leases.App.Models;

public record CreatePropertyDto
{
    [Required]
    [StringLength(200)]
    public required string Name { get; init; }

    [Required]
    [StringLength(500)]
    public required string Address { get; init; }

    [Required]
    public required string TimeZoneId { get; init; }

    public CreatePropertyRequest ToRequest()
    {
        return new CreatePropertyRequest
        {
            Name = Name,
            Address = Address,
            TimeZoneId = TimeZoneId,
        };
    }
}
