namespace MagicCSharp.Generators.Tests;

public record TestSourceFile
{
    public required string Path { get; init; }

    public required string Text { get; init; }
}
