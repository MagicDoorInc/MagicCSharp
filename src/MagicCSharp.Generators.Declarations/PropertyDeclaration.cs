using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

public sealed record PropertyDeclaration
{
    public required string Name { get; init; }

    public required TypeReference Type { get; init; }

    public required Accessibility Accessibility { get; init; }

    public required bool IsRequired { get; init; }

    public required bool HasGetter { get; init; }

    public required SetterKind SetterKind { get; init; }

    /// <summary>The type that declares the property, <c>global::</c>-qualified; a base type's for an inherited one.</summary>
    public required string DeclaringTypeFullyQualifiedName { get; init; }

    public required DocumentationComment Documentation { get; init; }

    public required EquatableArray<AttributeDeclaration> Attributes { get; init; }

    public required SourceLocation? Location { get; init; }
}
