using MagicCSharp.Infrastructure.Exceptions;

namespace MagicCSharp.Data.Repositories;

/// <summary>
///     Convenience over <see cref="IRepository{TEntity,TKey,TEdit,TFilter}" />.
/// </summary>
public static class RepositoryExtensions
{
    /// <summary>
    ///     Get one entity by key, or throw <see cref="NotFoundException" /> when it does not exist.
    ///     <para>
    ///         <c>Get</c> returns null, which is right for a caller that has something to do about it, and
    ///         wrong for the far more common one that does not. <c>Update</c> and <c>Delete</c> already
    ///         throw for a missing key; this makes reads agree with them, and since error handling turns
    ///         the exception into a 404, an endpoint that fetches by id needs no null branch at all.
    ///     </para>
    ///     <para>
    ///         The exception carries the key, so the log line says which order was missing rather than
    ///         only that one was.
    ///     </para>
    /// </summary>
    /// <exception cref="NotFoundException">No entity has this key.</exception>
    public static async Task<TEntity> GetOrThrow<TEntity, TKey, TEdit, TFilter>(
        this IRepository<TEntity, TKey, TEdit, TFilter> repository,
        TKey key)
        where TKey : IEquatable<TKey>
    {
        var entity = await repository.Get(key);

        if (entity == null)
        {
            throw Missing<TEntity, TKey>(key);
        }

        return entity;
    }

    /// <summary>
    ///     The most specific not-found exception the key type allows, so <c>GetDebugData</c> has something
    ///     to report.
    /// </summary>
    private static NotFoundException Missing<TEntity, TKey>(TKey key)
    {
        var name = typeof(TEntity).Name;

        return key switch
        {
            long id => new NotFoundIdException(id, name),
            int id => new NotFoundIdException(id, name),
            string text => new NotFoundKeyException(text, name),
            _ => new NotFoundKeyException(key?.ToString() ?? "", name),
        };
    }
}
