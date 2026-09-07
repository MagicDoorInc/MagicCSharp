using MagicCSharp.Data.Models;

namespace MagicCSharp.Data.Repositories;

/// <summary>
///     Opt-in page-at-a-time reads, for entities whose result sets are too large to return whole.
///     Implement alongside <see cref="IRepository{TEntity,TKey,TEdit,TFilter}" />.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TFilter">The query filter type.</typeparam>
public interface IPaginatedRepository<TEntity, TFilter>
{
    /// <summary>
    ///     Get one page of the entities matching the filter, plus the total count so a caller can render
    ///     "page 3 of 12" without a second query.
    /// </summary>
    Task<Pagination<TEntity>> Get(PaginationRequest pagination, TFilter filter);
}
