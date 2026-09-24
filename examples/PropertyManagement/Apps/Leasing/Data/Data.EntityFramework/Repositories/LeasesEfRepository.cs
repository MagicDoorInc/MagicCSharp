using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Utils;
using MagicCSharp.Infrastructure.KeyGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Acme.Leasing.Data.EntityFramework.Dals;
using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;

namespace Acme.Leasing.Data.EntityFramework.Repositories;

public class LeasesEfRepository(
    IDbContextFactory<MagicLeasingContext> contextFactory,
    IKeyGenService keyGenService,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
    : BaseIdPaginatedRepository<MagicLeasingContext, LeaseDal, Lease, LeaseFilter, LeaseEdit>(
        contextFactory, timeProvider, loggerFactory), ILeasesRepository
{
    protected override LeaseDal CreateDal(LeaseEdit edit)
    {
        return LeaseDal.From(edit, keyGenService.GetId());
    }

    protected override IQueryable<LeaseDal> ApplyFilter(IQueryable<LeaseDal> query, LeaseFilter filter)
    {
        query = query.ApplyListFilter(filter.Ids, x => (long?)x.Id);
        query = query.ApplyComparableRangeFilter(filter.Created, x => (DateTimeOffset?)x.Created);
        query = query.ApplyListFilter(filter.PropertyIds, x => (long?)x.PropertyId);

        return query;
    }
}
