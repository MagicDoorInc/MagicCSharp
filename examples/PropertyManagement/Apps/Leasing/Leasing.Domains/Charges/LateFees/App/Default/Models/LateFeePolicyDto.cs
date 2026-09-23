using Acme.Leasing.Domains.Charges.LateFees.Models.Entities;

namespace Acme.Leasing.Domains.Charges.LateFees.App.Models;

public record LateFeePolicyDto
{
    public required string PropertyId { get; init; }
    public required int GraceDays { get; init; }
    public required decimal Amount { get; init; }

    public static LateFeePolicyDto FromEntity(LateFeePolicy lateFeePolicy)
    {
        return new LateFeePolicyDto
        {
            PropertyId = lateFeePolicy.PropertyId.ToString(),
            GraceDays = lateFeePolicy.GraceDays,
            Amount = lateFeePolicy.Amount,
        };
    }
}
