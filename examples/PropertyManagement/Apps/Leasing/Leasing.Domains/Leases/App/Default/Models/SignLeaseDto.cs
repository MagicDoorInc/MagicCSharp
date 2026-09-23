using System.ComponentModel.DataAnnotations;
using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Libraries.Web.Models;

namespace Acme.Leasing.Domains.Leases.App.Models;

public record SignLeaseDto
{
    [Required]
    public required string PropertyId { get; init; }

    [Required]
    [StringLength(200)]
    public required string TenantName { get; init; }

    [Required]
    [EmailAddress]
    [StringLength(320)]
    public required string TenantEmail { get; init; }

    public required decimal MonthlyRent { get; init; }
    public required decimal SecurityDeposit { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }

    public SignLeaseRequest ToRequest()
    {
        return new SignLeaseRequest
        {
            PropertyId = PropertyId.ToId(),
            TenantName = TenantName,
            TenantEmail = TenantEmail,
            MonthlyRent = MonthlyRent,
            SecurityDeposit = SecurityDeposit,
            StartDate = StartDate,
            EndDate = EndDate,
        };
    }
}
