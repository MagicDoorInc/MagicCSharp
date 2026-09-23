using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Acme.Leasing.Data.EntityFramework.Dals;
using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Charges.LateFees.Models.Entities;

namespace Acme.Leasing.Data.EntityFramework.Repositories;

public class LateFeePoliciesEfRepository(
    IDbContextFactory<MagicLeasingContext> contextFactory,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
    : BaseIdRepository<MagicLeasingContext, LateFeePolicyDal, LateFeePolicy, LateFeePolicyFilter, LateFeePolicyEdit>(
        contextFactory, timeProvider, loggerFactory), ILateFeePoliciesRepository
{
    protected override LateFeePolicyDal CreateDal(LateFeePolicyEdit edit)
    {
        return LateFeePolicyDal.From(edit);
    }

    protected override IQueryable<LateFeePolicyDal> ApplyFilter(IQueryable<LateFeePolicyDal> query, LateFeePolicyFilter filter)
    {
        query = query.ApplyListFilter(filter.Ids, x => (long?)x.Id);
        query = query.ApplyComparableRangeFilter(filter.Created, x => (DateTimeOffset?)x.Created);

        return query;
    }
}
