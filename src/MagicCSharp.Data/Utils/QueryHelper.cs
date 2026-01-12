using MagicCSharp.Data.Models;

namespace MagicCSharp.Data.Utils;

/// <summary>
///     Helper methods for working with IQueryable data.
/// </summary>
public static class QueryHelper
{
    /// <summary>
    ///     Apply pagination to a query.
    /// </summary>
    /// <typeparam name="TDal">The DAL type.</typeparam>
    /// <param name="query">The query to paginate.</param>
    /// <param name="paginationRequest">The pagination request.</param>
    /// <returns>The paginated query.</returns>
    public static IQueryable<TDal> ApplyPagination<TDal>(IQueryable<TDal> query, PaginationRequest paginationRequest)
        where TDal : class
    {
        if (paginationRequest.Disable || paginationRequest.PageSize == 0)
        {
            return query;
        }

        query = query.Skip(paginationRequest.Skip).Take(paginationRequest.PageSize);
        return query;
    }
}