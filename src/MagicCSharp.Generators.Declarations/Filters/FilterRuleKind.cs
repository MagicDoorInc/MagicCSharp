namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     How a filter field filters its entity property. Each kind is named after its query helper
///     (<c>Apply{Kind}Filter</c>) and its in-memory twin (<c>Matches{Kind}Filter</c>).
/// </summary>
public enum FilterRuleKind
{
    /// <summary>The property's value is one of the listed values; <c>Ids</c> filters <c>Id</c>.</summary>
    List,

    /// <summary>The property equals the field's value; <c>TenantId</c> filters <c>TenantId</c>.</summary>
    NullableValue,

    /// <summary>The nullable property has a value, or has none; <c>IsDeleted</c> filters <c>Deleted</c>.</summary>
    NullablePresence,

    /// <summary>The property lies in the range; <c>CreatedRange</c> (or <c>Created</c>) filters <c>Created</c>.</summary>
    ComparableRange,
}
