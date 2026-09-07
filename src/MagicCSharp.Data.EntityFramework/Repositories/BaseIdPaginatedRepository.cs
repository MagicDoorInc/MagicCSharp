using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Data.Models;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Data.Utils;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Repositories;

/// <summary>
///     A <see cref="BaseIdRepository{TContext,TDal,TEntity,TFilter,TEdit}" /> that can also return one page at a
///     time. Derive from this when the entity's result sets grow without bound; derive from the plain base when
///     they do not, so the contract says so.
/// </summary>
public abstract class BaseIdPaginatedRepository<TContext, TDal, TEntity, TFilter, TEdit>(
    IDbContextFactory<TContext> contextFactory,
    IClock clock,
    ILoggerFactory loggerFactory)
    : BaseIdRepository<TContext, TDal, TEntity, TFilter, TEdit>(contextFactory, clock, loggerFactory), IPaginatedRepository<TEntity, TFilter>
    where TDal : class, IDalTransform<TEntity, TEdit>, IDalId, IDal
    where TEntity : class, TEdit, IIdEntity
    where TContext : DbContext
{
    public virtual async Task<Pagination<TEntity>> Get(PaginationRequest pagination, TFilter filter)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        var query = GetQueryNoTracking(context);
        query = ApplyFilter(query, filter);
        query = ApplyOrder(query);

        // Count before paging, so the caller gets the size of the whole result rather than of this page.
        var total = await query.CountAsync();

        query = QueryHelper.ApplyPagination(query, pagination);
        var dals = await query.ToListAsync();

        return new Pagination<TEntity>(pagination, total, dals.Select(x => x.ToEntity()).ToList());
    }
}
