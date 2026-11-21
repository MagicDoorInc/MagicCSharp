namespace MagicCSharp.Generators.Declarations;

/// <summary>An attribute argument given by name: <c>Service = "Maintenance"</c>.</summary>
public sealed record NamedAttributeArgument
{
    public required string Name { get; init; }

    public required AttributeArgument Value { get; init; }
}
