using System.Collections;
using System.Collections.Immutable;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     An immutable array compared by its items. Incremental generators skip work when a pipeline step returns a
///     value equal to the last run's; an <see cref="ImmutableArray{T}" /> compares by reference, so a model holding
///     one would never be equal and every keystroke would regenerate everything.
/// </summary>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> items;

    public EquatableArray(IEnumerable<T> items)
    {
        this.items = items.ToImmutableArray();
    }

    public static EquatableArray<T> Empty { get; } = new EquatableArray<T>(ImmutableArray<T>.Empty);

    public int Count => Items.Length;

    public T this[int index] => Items[index];

    // default(EquatableArray<T>) is a valid empty array rather than a crash on first use.
    private ImmutableArray<T> Items => items.IsDefault ? ImmutableArray<T>.Empty : items;

    public bool Equals(EquatableArray<T> other)
    {
        return Items.SequenceEqual(other.Items);
    }

    public override bool Equals(object? other)
    {
        if (other is EquatableArray<T>)
        {
            return Equals((EquatableArray<T>)other);
        }

        return false;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in Items)
            {
                hash = (hash * 31) + (item == null ? 0 : item.GetHashCode());
            }

            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)Items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }
}
