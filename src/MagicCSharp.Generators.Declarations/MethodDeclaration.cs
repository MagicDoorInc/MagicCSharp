using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

public sealed record MethodDeclaration
{
    public required string Name { get; init; }

    public required TypeReference ReturnType { get; init; }

    public required EquatableArray<ParameterDeclaration> Parameters { get; init; }

    public required EquatableArray<string> TypeParameters { get; init; }

    /// <summary>
    ///     One <c>where</c> clause per constrained type parameter, as C# to write back:
    ///     <c>where TKey : notnull, global::System.IComparable&lt;TKey&gt;</c>. Empty when none is constrained.
    /// </summary>
    public EquatableArray<string> TypeParameterConstraintClauses { get; init; } = EquatableArray<string>.Empty;

    public required Accessibility Accessibility { get; init; }

    public required bool IsStatic { get; init; }

    public required bool IsOverride { get; init; }

    public required bool IsAbstract { get; init; }

    public required DocumentationComment Documentation { get; init; }

    public required EquatableArray<AttributeDeclaration> Attributes { get; init; }

    public required SourceLocation? Location { get; init; }
}
