namespace MagicCSharp.Data.EntityFramework.Dals;

/// <summary>
///     An id-keyed DAL that builds a new row from an edit and the id it is given. The repository bases use it
///     when a repository does not override <c>CreateDal</c>. A <see cref="GenerateDalMappingAttribute" /> DAL that
///     writes neither <c>ToEntity</c> nor <c>Apply</c> gets it, and the <c>From</c> behind it, from
///     MagicCSharp.Generators; one that writes either lists it to get the generated <c>From</c>.
/// </summary>
/// <typeparam name="TDal">The DAL itself.</typeparam>
/// <typeparam name="TEdit">The edit it is built from.</typeparam>
public interface IDalFromEdit<TDal, TEdit>
    where TDal : IDalFromEdit<TDal, TEdit>
{
    /// <summary>
    ///     A new row: the id, the columns set only at creation, then everything <c>Apply</c> sets.
    /// </summary>
    /// <param name="edit">The fields of the new row.</param>
    /// <param name="id">The id the row is created with.</param>
    static abstract TDal From(TEdit edit, long id);
}
