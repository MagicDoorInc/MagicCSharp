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
///     A <see cref="BaseKeyRepository{TContext,TDal,TEntity,TFilter,TEdit}" /> that can also return one page at a
///     time.
/// </summary>
public abstract class BaseKeyPaginatedRepository<TContext, TDal, TEntity, TFilter, TEdit>
    : BaseKeyRepository<TContext, TDal, TEntity, TFilter, TEdit>, IPaginatedRepository<TEntity, TFilter>
    where TDal : class, IDalTransform<TEntity, TEdit>, IDalKey, IDal
    where TEntity : class, TEdit, IKeyEntity
    where TContext : DbContext
{
    protected BaseKeyPaginatedRepository(IDbContextFactory<TContext> contextFactory, TimeProvider timeProvider, ILoggerFactory loggerFactory)
        : base(contextFactory, timeProvider, loggerFactory)
    {
    }

    /// <summary>Everything from one <see cref="RepositoryServices{TContext}" />, the key service included.</summary>
    protected BaseKeyPaginatedRepository(RepositoryServices<TContext> repositoryServices)
        : base(repositoryServices)
    {
    }

    public virtual async Task<Pagination<TEntity>> Get(PaginationRequest pagination, TFilter filter)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        var query = GetQueryNoTracking(context);
        query = ApplyFilter(query, filter);
        query = ApplyOrder(query);

        var total = await query.CountAsync();

        query = QueryHelper.ApplyPagination(query, pagination);
        var dals = await query.ToListAsync();

        return new Pagination<TEntity>(pagination, total, dals.Select(x => x.ToEntity()).ToList());
    }
}
