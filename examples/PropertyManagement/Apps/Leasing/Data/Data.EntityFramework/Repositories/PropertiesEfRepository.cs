using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Utils;
using MagicCSharp.Infrastructure.KeyGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Acme.Leasing.Data.EntityFramework.Dals;
using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;

namespace Acme.Leasing.Data.EntityFramework.Repositories;

public class PropertiesEfRepository(
    IDbContextFactory<MagicLeasingContext> contextFactory,
    IKeyGenService keyGenService,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
    : BaseIdPaginatedRepository<MagicLeasingContext, PropertyDal, Property, PropertyFilter, PropertyEdit>(
        contextFactory, timeProvider, loggerFactory), IPropertiesRepository
{
    protected override PropertyDal CreateDal(PropertyEdit edit)
    {
        return PropertyDal.From(edit, keyGenService.GetId());
    }

    protected override IQueryable<PropertyDal> ApplyFilter(IQueryable<PropertyDal> query, PropertyFilter filter)
    {
        query = query.ApplyListFilter(filter.Ids, x => (long?)x.Id);
        query = query.ApplyComparableRangeFilter(filter.Created, x => (DateTimeOffset?)x.Created);

        return query;
    }
}
