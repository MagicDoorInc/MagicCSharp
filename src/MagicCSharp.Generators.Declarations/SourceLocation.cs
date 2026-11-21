using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Where a declaration sits in source, kept as plain values: a Roslyn <see cref="Location" /> holds its syntax
///     tree, which differs on every run and would defeat the incremental cache.
/// </summary>
public sealed record SourceLocation
{
    public required string FilePath { get; init; }

    public required TextSpan Span { get; init; }

    public required LinePositionSpan LineSpan { get; init; }

    /// <summary>The source location of <paramref name="location" />, or null when it is not in source (metadata).</summary>
    public static SourceLocation? From(Location? location)
    {
        if (location == null || !location.IsInSource)
        {
            return null;
        }

        return new SourceLocation
        {
            FilePath = location.SourceTree?.FilePath ?? "",
            Span = location.SourceSpan,
            LineSpan = location.GetLineSpan().Span,
        };
    }

    public Location ToLocation()
    {
        return Location.Create(FilePath, Span, LineSpan);
    }
}
