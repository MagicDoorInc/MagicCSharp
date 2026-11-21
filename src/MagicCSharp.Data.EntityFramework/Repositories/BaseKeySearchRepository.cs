using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Data.Utils;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Repositories;

/// <summary>
///     A paginated key-keyed repository with a denormalized free-text search column. See
///     <see cref="BaseIdSearchRepository{TContext,TDal,TEntity,TFilter,TEdit}" /> for how the column is kept
///     current and queried.
/// </summary>
public abstract class BaseKeySearchRepository<TContext, TDal, TEntity, TFilter, TEdit>
    : BaseKeyPaginatedRepository<TContext, TDal, TEntity, TFilter, TEdit>, ISearchRepository<string>
    where TDal : class, IDalTransform<TEntity, TEdit>, IDalKey, IDal, IDalSearchField
    where TEntity : class, TEdit, IKeyEntity
    where TContext : DbContext
{
    protected BaseKeySearchRepository(IDbContextFactory<TContext> contextFactory, TimeProvider timeProvider, ILoggerFactory loggerFactory)
        : base(contextFactory, timeProvider, loggerFactory)
    {
    }

    /// <summary>Everything from one <see cref="RepositoryServices{TContext}" />, the key service included.</summary>
    protected BaseKeySearchRepository(RepositoryServices<TContext> repositoryServices)
        : base(repositoryServices)
    {
    }

    /// <inheritdoc cref="BaseIdSearchRepository{TContext,TDal,TEntity,TFilter,TEdit}.SearchSynonyms" />
    protected virtual IReadOnlyDictionary<string, string>? SearchSynonyms => null;

    public virtual async Task UpdateSearch(string key, IReadOnlyList<string> keywords)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        var dal = await GetQuery(context).FirstOrDefaultAsync(x => x.Key == key) ?? throw GetNotFoundException(key);

        dal.MetaDataSearch = SearchText.Normalize(keywords, SearchSynonyms);

        await context.SaveChangesAsync();
    }

    /// <inheritdoc cref="BaseIdSearchRepository{TContext,TDal,TEntity,TFilter,TEdit}.ApplySearch" />
    protected IQueryable<TDal> ApplySearch(IQueryable<TDal> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        query = query.Where(x => x.MetaDataSearch != null);

        foreach (var word in SearchText.SplitQuery(search, SearchSynonyms))
        {
            var term = word;
            query = query.Where(x => x.MetaDataSearch!.Contains(term));
        }

        return query;
    }
}
