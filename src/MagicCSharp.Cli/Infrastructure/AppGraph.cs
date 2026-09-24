namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     One app — a deployable service under <c>Apps/</c> — and the folders a change in which affects it.
/// </summary>
public record AppGraph
{
    public required string Name { get; init; }

    /// <summary>
    ///     Repository-relative paths with forward slashes — the app's own folder and the folder of every project it
    ///     depends on — each matching itself and anything beneath it.
    /// </summary>
    public required IReadOnlyList<string> Folders { get; init; }
}
