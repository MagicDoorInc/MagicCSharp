namespace MagicCSharp.Data.Repositories;

/// <summary>
///     Opt-in free-text search over an entity, without a search engine.
///     <para>
///         The repository keeps one denormalized column of normalized keywords per row. The caller decides what is
///         worth searching — a lease's tenant names, its unit's address, its portfolio — and hands those strings to
///         <see cref="UpdateSearch" /> whenever any of them changes. A search then matches against that one column
///         rather than joining across everything it might have come from.
///     </para>
///     <para>
///         Because the column is a copy, it goes stale unless every write path that touches a contributing value
///         calls <see cref="UpdateSearch" /> again.
///     </para>
/// </summary>
/// <typeparam name="TKey">The entity's primary key type.</typeparam>
public interface ISearchRepository<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    ///     Replace the stored search keywords for one entity. The keywords are normalized before storage, so the
    ///     caller can pass raw values as they appear on the entity.
    /// </summary>
    /// <exception cref="Infrastructure.Exceptions.NotFoundException">No entity has this key.</exception>
    Task UpdateSearch(TKey key, IReadOnlyList<string> keywords);
}
