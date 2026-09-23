using Acme.Leasing.Domains.Charges.LateFees.UseCases;

namespace Acme.Leasing.Domains.Charges.LateFees.App.Models;

public record ApplyLateFeesResultDto
{
    public required int LateFeesApplied { get; init; }

    public static ApplyLateFeesResultDto FromResult(ApplyLateFeesResult result)
    {
        return new ApplyLateFeesResultDto
        {
            LateFeesApplied = result.LateFeesApplied,
        };
    }
}
