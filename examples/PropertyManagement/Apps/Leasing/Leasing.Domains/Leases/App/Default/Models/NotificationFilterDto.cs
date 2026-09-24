using Acme.Leasing.Domains.Leases.Models.Entities;
using Acme.Libraries.Web.Models;

namespace Acme.Leasing.Domains.Leases.App.Models;

public record NotificationFilterDto
{
    public List<string>? LeaseIds { get; init; }

    public NotificationFilter ToFilter()
    {
        return new NotificationFilter
        {
            LeaseIds = LeaseIds?.ToIds(),
        };
    }
}
