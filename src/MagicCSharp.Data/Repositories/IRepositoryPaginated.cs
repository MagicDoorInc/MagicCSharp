using MagicCSharp.Data.Models;

namespace MagicCSharp.Data.Repositories;

/// <summary>
/// Repository interface with pagination support.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TFilter">The filter type for queries.</typeparam>
/// <typeparam name="TEdit">The edit type for create/update operations.</typeparam>
public interface IRepositoryPaginated<TEntity, in TFilter, in TEdit> : IRepository<TEntity, TFilter, TEdit>
{
    /// <summary>
    /// Get a paginated result of entities matching the filter.
    /// </summary>
    Task<Pagination<TEntity>> Get(PaginationRequest pagination, TFilter filter);
}
