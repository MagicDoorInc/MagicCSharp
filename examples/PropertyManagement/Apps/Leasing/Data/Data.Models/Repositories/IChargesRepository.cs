using MagicCSharp.Data.Repositories;
using Acme.Leasing.Domains.Charges.Models.Entities;

namespace Acme.Leasing.Data.Repositories;

public interface IChargesRepository :
    IRepository<Charge, long, ChargeEdit, ChargeFilter>,
    IPaginatedRepository<Charge, ChargeFilter>
{
}
