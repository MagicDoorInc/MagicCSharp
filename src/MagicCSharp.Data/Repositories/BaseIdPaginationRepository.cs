using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MagicCSharp.Data.Dals;
using MagicCSharp.Data.Models;
using MagicCSharp.Data.Utils;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Data.Repositories;

/// <summary>
/// Base repository implementation with pagination support for entities with numeric ID identifiers.
/// </summary>
/// <typeparam name="TContext">The database context type.</typeparam>
/// <typeparam name="TDal">The data access layer type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TFilter">The filter type for queries.</typeparam>
/// <typeparam name="TEdit">The edit type for create/update operations.</typeparam>
public abstract class BaseIdPaginationRepository<TContext, TDal, TEntity, TFilter, TEdit> : BaseIdRepository<TContext, TDal, TEntity, TFilter, TEdit>,
    IRepositoryPaginated<TEntity, TFilter, TEdit>
    where TDal : class, IDalTransform<TEntity, TEdit>, IDalId, IDal
    where TEntity : class, TEdit, IIdEntity
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> contextFactory;

    protected BaseIdPaginationRepository(
        IDbContextFactory<TContext> contextFactory,
        IClock clock,
        ILogger logger) : base(contextFactory, clock, logger)
    {
        this.contextFactory = contextFactory;
    }

    public async Task<Pagination<TEntity>> Get(PaginationRequest pagination, TFilter filter)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var query = GetQuery(context);
        query = ApplyFilter(query, filter);
        query = ApplyOrder(query);
        var total = await query.CountAsync();
        query = QueryHelper.ApplyPagination(query, pagination);
        var dals = await query.ToListAsync();
        return new Pagination<TEntity>(pagination, total, dals.Select(x => ToEntity(x)).ToList());
    }
}
