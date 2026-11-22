namespace MagicCSharp.Data.EntityFramework.Repositories;

/// <summary>
///     Asks <c>MagicCSharp.Generators</c> to write this repository's <c>ApplyFilter</c>: one query helper call per
///     filter field, read from the field's name and type. The repository must be <c>partial</c>, concrete, and inherit an
///     abstract <c>ApplyFilter(IQueryable&lt;TDal&gt; query, TFilter filter)</c> over a DAL implementing
///     <see cref="Dals.IDalTransform{TEntity,TEntityEdit}" />. A repository that writes <c>ApplyFilter</c> itself is
///     left alone, though it must still be <c>partial</c>. A filter field no name rule matches is filtered in
///     <c>private partial IQueryable&lt;TDal&gt; ApplyCustomFilter(IQueryable&lt;TDal&gt; query, TFilter filter)</c>.
/// </summary>
/// <example>
///     <code language="csharp">
///     [GenerateFilterQuery]
///     public partial class NotesRepository(RepositoryServices&lt;NotesContext&gt; repositoryServices)
///         : BaseIdRepository&lt;NotesContext, NoteDal, Note, NoteFilter, NoteEdit&gt;(repositoryServices);
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateFilterQueryAttribute : Attribute
{
}
