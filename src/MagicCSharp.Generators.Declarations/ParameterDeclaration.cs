using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

public sealed record ParameterDeclaration
{
    public required string Name { get; init; }

    public required TypeReference Type { get; init; }

    public required RefKind RefKind { get; init; }

    public required bool IsParams { get; init; }

    /// <summary>The default value as C# to write back (<c>default</c>, <c>null</c>, <c>"x"</c>, <c>global::E.Member</c>); null when there is none.</summary>
    public required string? DefaultValueExpression { get; init; }

    public required EquatableArray<AttributeDeclaration> Attributes { get; init; }
}
