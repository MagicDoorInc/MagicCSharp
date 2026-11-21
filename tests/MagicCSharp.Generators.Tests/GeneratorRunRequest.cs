using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Tests;

public record GeneratorRunRequest
{
    public required TestSourceFile[] SourceFiles { get; init; }

    /// <summary>The MSBuild property MagicCSharpUseCaseMarkerInterface; null leaves it unset.</summary>
    public string? MarkerInterfaceSetting { get; init; }

    /// <summary>Other MSBuild properties the project passes to generators, by name without <c>build_property.</c>.</summary>
    public IReadOnlyDictionary<string, string> BuildProperties { get; init; } = new Dictionary<string, string>();

    /// <summary>Generators that run beside <see cref="GenerateInterfaceGenerator" />.</summary>
    public IIncrementalGenerator[] OtherGenerators { get; init; } = [];

    /// <summary>Assemblies the subject compiles against besides the defaults (the platform and MagicCSharp).</summary>
    public MetadataReference[] AdditionalReferences { get; init; } = [];
}
