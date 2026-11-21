using MagicCSharp.Generators.Declarations;

namespace MagicCSharp.Generators.Tests;

/// <summary>A <c>[DescribeUseCase]</c> class and the use case interface its attribute names.</summary>
public record DescribedUseCase
{
    public required TypeDeclaration DescriptionClass { get; init; }

    public required AttributeArgument NamedInterface { get; init; }
}
