namespace MagicCSharp.Generators.Declarations.ContextFactories;

/// <summary>The class generated factories derive from, as resolved in the compilation.</summary>
public sealed record ContextFactoryBase
{
    /// <summary>The metadata name the build asked for.</summary>
    public required string MetadataName { get; init; }

    /// <summary>Whether the compilation has the type.</summary>
    public required bool IsFound { get; init; }

    /// <summary>Why it cannot be derived from; null when it can.</summary>
    public required string? UnusableReason { get; init; }

    /// <summary><c>global::</c>-qualified name without type arguments, for the generated base list.</summary>
    public required string FullyQualifiedNameWithoutTypeArguments { get; init; }

    /// <summary><c>global::</c>-qualified base and interface constraints on its type parameter.</summary>
    public required EquatableArray<string> ContextConstraints { get; init; }
}
