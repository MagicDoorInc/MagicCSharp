namespace MagicCSharp.Generators.Declarations;

/// <summary>An attribute on a declaration, with its arguments.</summary>
public sealed record AttributeDeclaration
{
    public required TypeReference Type { get; init; }

    public required EquatableArray<AttributeArgument> ConstructorArguments { get; init; }

    public required EquatableArray<NamedAttributeArgument> NamedArguments { get; init; }
}
