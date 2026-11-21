using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>A class, record, struct, interface or enum, as much of it as generators read.</summary>
public sealed record TypeDeclaration
{
    public required string Name { get; init; }

    /// <summary>The containing namespace, empty for the global namespace.</summary>
    public required string Namespace { get; init; }

    /// <summary><c>global::</c>-qualified.</summary>
    public required string FullyQualifiedName { get; init; }

    public required TypeReferenceKind Kind { get; init; }

    public required Accessibility Accessibility { get; init; }

    /// <summary>Every source declaration of the type is <c>partial</c>.</summary>
    public required bool IsPartial { get; init; }

    public required bool IsAbstract { get; init; }

    public required bool IsStatic { get; init; }

    public required bool IsNested { get; init; }

    public required EquatableArray<string> TypeParameters { get; init; }

    /// <summary>The base class chain, nearest first, <c>object</c> left out.</summary>
    public required EquatableArray<TypeReference> BaseTypes { get; init; }

    /// <summary>The interfaces the type lists itself.</summary>
    public required EquatableArray<TypeReference> Interfaces { get; init; }

    /// <summary>Every interface the type implements, through its bases and other interfaces too.</summary>
    public required EquatableArray<TypeReference> AllInterfaces { get; init; }

    /// <summary>Public instance properties, the type's own and inherited; a redeclared one appears once, as the type sees it.</summary>
    public required EquatableArray<PropertyDeclaration> Properties { get; init; }

    /// <summary>Ordinary methods the type declares itself, any accessibility, in declaration order.</summary>
    public required EquatableArray<MethodDeclaration> Methods { get; init; }

    public required EquatableArray<AttributeDeclaration> Attributes { get; init; }

    public required DocumentationComment Documentation { get; init; }

    public required SourceLocation? Location { get; init; }
}
