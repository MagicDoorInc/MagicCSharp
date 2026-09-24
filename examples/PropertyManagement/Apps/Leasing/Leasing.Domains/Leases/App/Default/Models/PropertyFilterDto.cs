using Acme.Leasing.Domains.Leases.Models.Entities;
using Acme.Libraries.Web.Models;

namespace Acme.Leasing.Domains.Leases.App.Models;

public record PropertyFilterDto
{
    public List<string>? Ids { get; init; }

    public PropertyFilter ToFilter()
    {
        return new PropertyFilter
        {
            Ids = Ids?.ToIds(),
        };
    }
}
