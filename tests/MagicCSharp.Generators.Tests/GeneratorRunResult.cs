using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Tests;

public record GeneratorRunResult
{
    public required TestSourceFile[] GeneratedFiles { get; init; }

    /// <summary>The generators' diagnostics, as "{Id} {File}:{Line} {Message}".</summary>
    public required string[] Diagnostics { get; init; }

    /// <summary>The subject with the generated files added; the harness has checked it compiles.</summary>
    public required Compilation OutputCompilation { get; init; }

    public string GeneratedText(string hintName)
    {
        return GeneratedFiles.Single(generatedFile => generatedFile.Path == hintName).Text;
    }
}
