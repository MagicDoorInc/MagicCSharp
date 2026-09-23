using MagicCSharp.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;

namespace Acme.Leasing.Data.Repositories;

public interface ILeasesRepository :
    IRepository<Lease, long, LeaseEdit, LeaseFilter>,
    IPaginatedRepository<Lease, LeaseFilter>
{
}
