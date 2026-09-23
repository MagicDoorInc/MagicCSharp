using Acme.Leasing.Domains.Charges.Models.Entities;

namespace Acme.Leasing.Domains.Charges.App.Models;

public record ChargeDto
{
    public required string Id { get; init; }
    public required string LeaseId { get; init; }
    public required ChargeType Type { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly DueDate { get; init; }
    public required DateTimeOffset? Paid { get; init; }
    public required string? LateFeeForChargeId { get; init; }

    public static ChargeDto FromEntity(Charge charge)
    {
        return new ChargeDto
        {
            Id = charge.Id.ToString(),
            LeaseId = charge.LeaseId.ToString(),
            Type = charge.Type,
            Amount = charge.Amount,
            DueDate = charge.DueDate,
            Paid = charge.Paid,
            LateFeeForChargeId = charge.LateFeeForChargeId?.ToString(),
        };
    }
}
