using MagicCSharp.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;

namespace Acme.Leasing.Data.Repositories;

public interface INotificationsRepository :
    IRepository<Notification, string, NotificationEdit, NotificationFilter>,
    IPaginatedRepository<Notification, NotificationFilter>
{
}
