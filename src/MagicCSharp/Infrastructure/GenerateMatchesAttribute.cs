namespace MagicCSharp.Infrastructure;

/// <summary>
///     Asks <c>MagicCSharp.Generators</c> to write this filter's <see cref="IEntityFilter{TEntity}.Matches" /> from the
///     same name rules that write its repository's query, so an in-memory test repository and the database cannot
///     disagree about which entities the filter returns. The filter must be <c>partial</c> and list
///     <see cref="IEntityFilter{TEntity}" />. A filter that writes <c>Matches</c> itself is left alone. A field no name
///     rule matches is checked in <c>private partial bool MatchesCustomFilter(TEntity entity)</c>.
/// </summary>
/// <example>
///     <code language="csharp">
///     [GenerateMatches]
///     public partial class NoteFilter : IEntityFilter&lt;Note&gt;
///     {
///         public long? TenantId { get; init; }
///     }
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateMatchesAttribute : Attribute
{
}
