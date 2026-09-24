using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Charges.Models.Entities;
using MagicCSharp.UseCases;
using Microsoft.Extensions.Logging;

namespace Acme.Leasing.Domains.Charges.UseCases;

public interface ICreateChargesUseCase : IMagicUseCase
{
    Task<IReadOnlyList<Charge>> Execute(IReadOnlyList<ChargeEdit> charges);
}

public class CreateChargesUseCase(
    IChargesRepository chargesRepository,
    ILogger<CreateChargesUseCase> logger) : ICreateChargesUseCase
{
    public async Task<IReadOnlyList<Charge>> Execute(IReadOnlyList<ChargeEdit> charges)
    {
        logger.LogTrace("Executing: charges={charges}", charges);

        foreach (var chargeEdit in charges)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chargeEdit.Amount, nameof(chargeEdit.Amount));
        }

        var chargeIds = await chargesRepository.Create(charges);
        return await chargesRepository.Get(chargeIds);
    }
}
