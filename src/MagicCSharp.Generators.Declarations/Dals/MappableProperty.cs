namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>
///     A public instance property of a Dal, entity or edit, own or inherited, with what the mapping may do with it.
/// </summary>
public sealed record MappableProperty
{
    public required string Name { get; init; }

    /// <summary>Fully qualified, with <c>?</c> when nullable; two properties have the same type when these are equal.</summary>
    public required string TypeName { get; init; }

    /// <summary>Has a public getter.</summary>
    public required bool IsReadable { get; init; }

    /// <summary>Has a public <c>set</c>, so a method body can assign it.</summary>
    public required bool IsSettable { get; init; }

    /// <summary>Has a public <c>set</c> or <c>init</c>, so an object initializer can assign it.</summary>
    public required bool IsInitializable { get; init; }

    /// <summary>Declared <c>required</c>.</summary>
    public required bool IsRequired { get; init; }

    /// <summary>Carries <c>[Key]</c>.</summary>
    public required bool IsKey { get; init; }

    /// <summary>Its type is <c>Nullable&lt;T&gt;</c>, a value type that may hold no value (<c>DateTimeOffset?</c>, not <c>string?</c>).</summary>
    public required bool IsNullableValueType { get; init; }
}
