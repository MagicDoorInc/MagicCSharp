using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Utils;
using MagicCSharp.Infrastructure.KeyGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Acme.Leasing.Data.EntityFramework.Dals;
using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Charges.Models.Entities;

namespace Acme.Leasing.Data.EntityFramework.Repositories;

public class ChargesEfRepository(
    IDbContextFactory<MagicLeasingContext> contextFactory,
    IKeyGenService keyGenService,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
    : BaseIdPaginatedRepository<MagicLeasingContext, ChargeDal, Charge, ChargeFilter, ChargeEdit>(
        contextFactory, timeProvider, loggerFactory), IChargesRepository
{
    protected override ChargeDal CreateDal(ChargeEdit edit)
    {
        return ChargeDal.From(edit, keyGenService.GetId());
    }

    protected override IQueryable<ChargeDal> ApplyFilter(IQueryable<ChargeDal> query, ChargeFilter filter)
    {
        query = query.ApplyListFilter(filter.Ids, x => (long?)x.Id);
        query = query.ApplyComparableRangeFilter(filter.Created, x => (DateTimeOffset?)x.Created);
        query = query.ApplyListFilter(filter.LeaseIds, x => (long?)x.LeaseId);
        query = query.ApplyListFilter(filter.Types, x => (ChargeType?)x.Type);
        query = query.ApplyNullableValueFilter(filter.IsPaid, x => (bool?)(x.Paid != null));
        query = query.ApplyComparableRangeFilter(filter.DueDate, x => (DateOnly?)x.DueDate);
        query = query.ApplyListFilter(filter.LateFeeForChargeIds, x => x.LateFeeForChargeId);

        return query;
    }
}
