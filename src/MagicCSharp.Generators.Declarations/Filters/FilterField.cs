namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>A public readable instance property of a filter, own or inherited.</summary>
public sealed record FilterField
{
    public required string Name { get; init; }

    public required FilterFieldShape Shape { get; init; }

    /// <summary>
    ///     The value the shape holds, fully qualified and not nullable: the list's element (<c>long</c>, <c>string</c>),
    ///     the nullable struct's value (<c>bool</c>) or the range's value (<c>global::System.DateTimeOffset</c>); null for
    ///     <see cref="FilterFieldShape.Custom" />.
    /// </summary>
    public required string? ValueTypeName { get; init; }

    /// <summary>The value type is <c>string</c>; false for a custom field.</summary>
    public bool IsStringValue { get; init; }

    /// <summary>The value type is <c>bool</c>; false for a custom field.</summary>
    public bool IsBooleanValue { get; init; }
}
