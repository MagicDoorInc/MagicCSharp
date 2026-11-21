namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>A filter field the name rules match to an entity property, and how it filters it.</summary>
public sealed record FilterFieldRule
{
    public required string FieldName { get; init; }

    /// <summary>The entity property it filters; the query filters the Dal column of this name.</summary>
    public required string PropertyName { get; init; }

    public required FilterRuleKind Kind { get; init; }

    /// <summary>
    ///     The helpers' value type argument, fully qualified and not nullable: the list's element, the value's type, or
    ///     for <see cref="FilterRuleKind.NullablePresence" /> the property's underlying type.
    /// </summary>
    public required string ValueTypeName { get; init; }

    /// <summary>
    ///     A list of strings. The helper for it has no value type argument, unlike the list helper over structs, so both
    ///     writers call it without one.
    /// </summary>
    public required bool IsStringList { get; init; }
}
