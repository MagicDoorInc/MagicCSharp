using Acme.Leasing.Domains.Charges.Models.Entities;
using Acme.Libraries.Web.Models;

namespace Acme.Leasing.Domains.Charges.App.Models;

public record ChargeFilterDto
{
    public List<string>? LeaseIds { get; init; }
    public List<ChargeType>? Types { get; init; }
    public bool? IsPaid { get; init; }

    public ChargeFilter ToFilter()
    {
        return new ChargeFilter
        {
            LeaseIds = LeaseIds?.ToIds(),
            Types = Types,
            IsPaid = IsPaid,
        };
    }
}
