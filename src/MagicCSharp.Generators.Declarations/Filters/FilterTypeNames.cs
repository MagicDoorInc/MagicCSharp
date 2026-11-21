namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     Metadata names of the MagicCSharp types the filter generators recognise, and the helper classes generated code
///     calls. Matched by name, so the generators need no reference to the runtime packages.
/// </summary>
public static class FilterTypeNames
{
    public const string EntityFilter = "MagicCSharp.Infrastructure.IEntityFilter`1";

    public const string ComparableRange = "MagicCSharp.Infrastructure.ComparableRange`1";

    public const string Queryable = "System.Linq.IQueryable`1";

    public const string Enumerable = "System.Collections.Generic.IEnumerable`1";

    /// <summary>The query helpers, called as static methods so a consumer's own same-named extensions cannot make a call ambiguous.</summary>
    public const string QueryFilterExtensions = "global::MagicCSharp.Data.Utils.QueryFilterExtensions";

    public const string FilterMatching = "global::MagicCSharp.Infrastructure.FilterMatching";
}
