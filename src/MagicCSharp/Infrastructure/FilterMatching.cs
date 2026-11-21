namespace MagicCSharp.Infrastructure;

/// <summary>
///     The in-memory twin of each query filter helper in <c>MagicCSharp.Data.Utils.QueryFilterExtensions</c>: the same
///     filter value selects the same entities in memory as the helper selects rows in the database, null and empty
///     included. Generated <c>Matches</c> methods call these.
/// </summary>
public static class FilterMatching
{
    /// <summary>
    ///     Twin of <c>ApplyListFilter</c>: no list matches everything, an empty list matches nothing, and otherwise the
    ///     value must be in the list; a null value never is, as a null column never equals a value in SQL.
    /// </summary>
    public static bool MatchesListFilter<TValue>(IEnumerable<TValue>? values, TValue? value)
        where TValue : struct
    {
        if (values == null)
        {
            return true;
        }

        return value != null && values.Contains(value.Value);
    }

    /// <summary>Twin of <c>ApplyListFilter</c> over strings, compared exactly (ordinal), as the database compares them.</summary>
    public static bool MatchesListFilter(IEnumerable<string>? values, string? value)
    {
        if (values == null)
        {
            return true;
        }

        return value != null && values.Contains(value, StringComparer.Ordinal);
    }

    /// <summary>Twin of <c>ApplyNullableValueFilter</c>: no filter value matches everything; otherwise the values are equal.</summary>
    public static bool MatchesNullableValueFilter<TValue>(TValue? filterValue, TValue? value)
        where TValue : struct
    {
        if (filterValue == null)
        {
            return true;
        }

        return value != null && EqualityComparer<TValue>.Default.Equals(value.Value, filterValue.Value);
    }

    /// <summary>
    ///     Twin of <c>ApplyNullablePresenceFilter</c>: null matches everything, true only entities that have a value,
    ///     false only entities that have none.
    /// </summary>
    public static bool MatchesNullablePresenceFilter<TValue>(bool? isPresent, TValue? value)
        where TValue : struct
    {
        if (isPresent == null)
        {
            return true;
        }

        return value.HasValue == isPresent.Value;
    }

    /// <summary>
    ///     Twin of <c>ApplyComparableRangeFilter</c>. A range with neither end adds no condition to the query, so it matches
    ///     every entity, one without a value included; a range with an end never matches a missing value.
    /// </summary>
    public static bool MatchesComparableRangeFilter<TValue>(ComparableRange<TValue>? range, TValue? value)
        where TValue : struct, IComparable<TValue>
    {
        if (range == null || (range.Start == null && range.End == null))
        {
            return true;
        }

        return value != null && range.Contains(value.Value);
    }
}
