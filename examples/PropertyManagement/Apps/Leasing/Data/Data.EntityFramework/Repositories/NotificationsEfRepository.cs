using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Acme.Leasing.Data.EntityFramework.Dals;
using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;

namespace Acme.Leasing.Data.EntityFramework.Repositories;

public class NotificationsEfRepository(
    IDbContextFactory<MagicLeasingContext> contextFactory,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
    : BaseKeyPaginatedRepository<MagicLeasingContext, NotificationDal, Notification, NotificationFilter, NotificationEdit>(
        contextFactory, timeProvider, loggerFactory), INotificationsRepository
{
    protected override NotificationDal CreateDal(NotificationEdit edit)
    {
        return NotificationDal.From(edit);
    }

    protected override IQueryable<NotificationDal> ApplyFilter(IQueryable<NotificationDal> query, NotificationFilter filter)
    {
        query = query.ApplyListFilter(filter.Keys, x => x.Key);
        query = query.ApplyComparableRangeFilter(filter.Created, x => (DateTimeOffset?)x.Created);
        query = query.ApplyListFilter(filter.LeaseIds, x => (long?)x.LeaseId);

        return query;
    }
}
