namespace MagicCSharp.Infrastructure;

/// <summary>
///     A filter that can test one entity in memory, with the meaning its repository's query gives it in the database.
///     <para>
///         Make the filter <c>partial</c> and list this interface, and MagicCSharp.Generators writes <c>Matches</c>
///         from the same name rules that write the repository's query, so an in-memory test repository and the
///         database cannot disagree about which entities a filter returns.
///     </para>
/// </summary>
/// <typeparam name="TEntity">The entity the filter selects.</typeparam>
public interface IEntityFilter<in TEntity>
{
    /// <summary>True when the entity passes every field of the filter.</summary>
    bool Matches(TEntity entity);
}
