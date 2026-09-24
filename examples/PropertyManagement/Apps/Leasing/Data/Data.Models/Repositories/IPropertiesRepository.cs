using MagicCSharp.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;

namespace Acme.Leasing.Data.Repositories;

public interface IPropertiesRepository :
    IRepository<Property, long, PropertyEdit, PropertyFilter>,
    IPaginatedRepository<Property, PropertyFilter>
{
}
