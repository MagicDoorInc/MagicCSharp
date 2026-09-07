using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Data.Utils;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Repositories;

/// <summary>
///     A paginated id-keyed repository with a denormalized free-text search column.
///     <para>
///         Call <see cref="UpdateSearch" /> from every write path that changes something the search should find.
///         The column is a copy, so it is only as fresh as the last such call.
///     </para>
///     <para>
///         Use <see cref="ApplySearch" /> inside <c>ApplyFilter</c> to turn a search string in the filter into
///         query predicates.
///     </para>
/// </summary>
public abstract class BaseIdSearchRepository<TContext, TDal, TEntity, TFilter, TEdit>(
    IDbContextFactory<TContext> contextFactory,
    IClock clock,
    ILoggerFactory loggerFactory)
    : BaseIdPaginatedRepository<TContext, TDal, TEntity, TFilter, TEdit>(contextFactory, clock, loggerFactory), ISearchRepository<long>
    where TDal : class, IDalTransform<TEntity, TEdit>, IDalId, IDal, IDalSearchField
    where TEntity : class, TEdit, IIdEntity
    where TContext : DbContext
{
    /// <summary>
    ///     Domain synonyms applied to both stored keywords and incoming queries — a map from the long form to the
    ///     short one, e.g. <c>"street" =&gt; "st"</c>. Empty by default.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, string>? SearchSynonyms => null;

    public virtual async Task UpdateSearch(long id, IReadOnlyList<string> keywords)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        var dal = await GetQuery(context).FirstOrDefaultAsync(x => x.Id == id) ?? throw GetNotFoundException(id);

        dal.MetaDataSearch = SearchText.Normalize(keywords, SearchSynonyms);

        // Deliberately not stamping Updated: the search column is derived from values that were already
        // saved, so refreshing it is not an edit to the entity.
        await context.SaveChangesAsync();
    }

    /// <summary>
    ///     Narrow a query to rows matching every word of <paramref name="search" />. A null or blank search
    ///     leaves the query untouched.
    /// </summary>
    protected IQueryable<TDal> ApplySearch(IQueryable<TDal> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        query = query.Where(x => x.MetaDataSearch != null);

        // One predicate per word, so all of them must be present. Combining them into a single Contains
        // would instead require the words to appear together in that order.
        foreach (var word in SearchText.SplitQuery(search, SearchSynonyms))
        {
            var term = word;
            query = query.Where(x => x.MetaDataSearch!.Contains(term));
        }

        return query;
    }
}
