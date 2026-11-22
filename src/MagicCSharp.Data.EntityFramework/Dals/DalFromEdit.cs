using System.Reflection;

namespace MagicCSharp.Data.EntityFramework.Dals;

/// <summary>
///     Calls <see cref="IDalFromEdit{TDal,TEdit}.From" /> where the caller cannot constrain <typeparamref name="TDal" />
///     to it — the repository bases, which existing DALs without a <c>From</c> also derive from.
/// </summary>
/// <typeparam name="TDal">The DAL to build.</typeparam>
/// <typeparam name="TEdit">The edit it is built from.</typeparam>
public static class DalFromEdit<TDal, TEdit>
{
    private static readonly Func<TEdit, long, TDal>? FromEdit = FindFromEdit();

    /// <summary>A new row from <typeparamref name="TDal" />'s own <c>From</c>.</summary>
    /// <param name="edit">The fields of the new row.</param>
    /// <param name="id">The id the row is created with.</param>
    /// <exception cref="InvalidOperationException">
    ///     <typeparamref name="TDal" /> does not implement <see cref="IDalFromEdit{TDal,TEdit}" />.
    /// </exception>
    public static TDal Create(TEdit edit, long id)
    {
        if (FromEdit == null)
        {
            throw new InvalidOperationException(
                $"{typeof(TDal).Name} has no From({typeof(TEdit).Name} edit, long id) to build a new row with. Mark it partial and " +
                $"[GenerateDalMapping] so MagicCSharp.Generators writes one (a DAL that writes ToEntity or Apply by hand also lists IDalFromEdit<{typeof(TDal).Name}, " +
                $"{typeof(TEdit).Name}>), implement that interface by hand, or override CreateDal in its repository.");
        }

        return FromEdit(edit, id);
    }

    private static Func<TEdit, long, TDal>? FindFromEdit()
    {
        var fromEditInterface = typeof(IDalFromEdit<,>).MakeGenericType(typeof(TDal), typeof(TEdit));
        if (!fromEditInterface.IsAssignableFrom(typeof(TDal)))
        {
            return null;
        }

        var callFrom = typeof(DalFromEdit<TDal, TEdit>).GetMethod(nameof(CallFrom), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(TDal));
        return callFrom.CreateDelegate<Func<TEdit, long, TDal>>();
    }

    // The constraint that lets TDal.From be called; FindFromEdit closes it over TDal once it has checked it holds.
    private static TConstrainedDal CallFrom<TConstrainedDal>(TEdit edit, long id)
        where TConstrainedDal : IDalFromEdit<TConstrainedDal, TEdit>
    {
        return TConstrainedDal.From(edit, id);
    }
}
