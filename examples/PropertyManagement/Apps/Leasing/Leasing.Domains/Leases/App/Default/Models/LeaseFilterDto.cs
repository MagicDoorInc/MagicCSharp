using Acme.Leasing.Domains.Leases.Models.Entities;
using Acme.Libraries.Web.Models;

namespace Acme.Leasing.Domains.Leases.App.Models;

public record LeaseFilterDto
{
    public List<string>? PropertyIds { get; init; }

    public LeaseFilter ToFilter()
    {
        return new LeaseFilter
        {
            PropertyIds = PropertyIds?.ToIds(),
        };
    }
}
