using Acme.Leasing.Domains.Charges.LateFees.UseCases;

namespace Acme.Leasing.Domains.Charges.LateFees.App.Models;

public record SetLateFeePolicyDto
{
    public required int GraceDays { get; init; }
    public required decimal Amount { get; init; }

    public SetLateFeePolicyRequest ToRequest()
    {
        return new SetLateFeePolicyRequest
        {
            GraceDays = GraceDays,
            Amount = Amount,
        };
    }
}
