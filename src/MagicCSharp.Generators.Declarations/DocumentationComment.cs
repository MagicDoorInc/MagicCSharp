namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     A doc comment as the lines a generator writes back: each line is the text after <c>///</c>, spacing kept,
///     so <c>/// &lt;summary&gt;</c> is the line <c> &lt;summary&gt;</c>.
/// </summary>
public sealed record DocumentationComment
{
    public static DocumentationComment Empty { get; } = new DocumentationComment { Lines = EquatableArray<string>.Empty };

    public required EquatableArray<string> Lines { get; init; }

    public bool IsEmpty => Lines.Count == 0;
}
