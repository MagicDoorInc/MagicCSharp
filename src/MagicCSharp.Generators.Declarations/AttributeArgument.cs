namespace MagicCSharp.Generators.Declarations;

/// <summary>One argument of an attribute usage.</summary>
public sealed record AttributeArgument
{
    public required AttributeArgumentKind Kind { get; init; }

    /// <summary>The argument as C# to write back: <c>"service-2-service/vendors"</c>, <c>typeof(global::…)</c>.</summary>
    public required string Expression { get; init; }

    /// <summary>A primitive's or enum's value as invariant text; null for a type, an array or null.</summary>
    public required string? Value { get; init; }

    /// <summary>The type named by <c>typeof</c>, or the enum's type; null otherwise.</summary>
    public required TypeReference? Type { get; init; }

    /// <summary>The items of an array argument; empty otherwise.</summary>
    public required EquatableArray<AttributeArgument> Elements { get; init; }
}
