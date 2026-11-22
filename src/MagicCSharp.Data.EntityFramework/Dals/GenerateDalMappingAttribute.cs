namespace MagicCSharp.Data.EntityFramework.Dals;

/// <summary>
///     Asks <c>MagicCSharp.Generators</c> to write this DAL's mapping: <c>ToEntity</c> and <c>Apply</c>, copying each
///     property to the property of the same name and type, and <c>From</c> when the DAL writes neither of the other two
///     or lists <see cref="IDalFromEdit{TDal,TEdit}" />. The DAL must be <c>partial</c> and implement
///     <see cref="IDalTransform{TEntity,TEntityEdit}" />, even when it writes every method itself; a method it writes
///     itself is left alone.
/// </summary>
/// <example>
///     <code language="csharp">
///     [Table("notes")]
///     [GenerateDalMapping]
///     public partial class NoteDal : BaseIdDal&lt;Note, NoteEdit&gt;
///     {
///         [Column("text")]
///         public string Text { get; set; } = "";
///     }
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateDalMappingAttribute : Attribute
{
}
