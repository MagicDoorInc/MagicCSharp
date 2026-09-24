using MagicCSharp.Data.Repositories;
using Acme.Leasing.Domains.Charges.LateFees.Models.Entities;

namespace Acme.Leasing.Data.Repositories;

public interface ILateFeePoliciesRepository :
    IRepository<LateFeePolicy, long, LateFeePolicyEdit, LateFeePolicyFilter>
{
}
