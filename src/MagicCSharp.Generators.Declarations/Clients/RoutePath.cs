using Microsoft.CodeAnalysis.CSharp;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>
///     A client path with <c>{name}</c> route values: <c>api/leases/{leaseId}/notes</c>. A value's name ends at a
///     constraint, default or optional marker (<c>{leaseId:long}</c> is <c>leaseId</c>).
/// </summary>
internal static class RoutePath
{
    private static readonly char[] NameEnds = [':', '?', '='];

    public static List<string> PlaceholderNames(string path)
    {
        return Pieces(path).Where(piece => piece.IsPlaceholder).Select(piece => piece.Text).ToList();
    }

    /// <summary>The C# that builds the path, each route value written by its expression in <paramref name="routeExpressions" />.</summary>
    public static string Expression(string path, IReadOnlyList<KeyValuePair<string, string>> routeExpressions)
    {
        var parts = Pieces(path).Select(piece => piece.IsPlaceholder ? ExpressionFor(piece.Text, routeExpressions) : SymbolDisplay.FormatLiteral(piece.Text, true))
            .ToList();
        if (parts.Count == 0)
        {
            return "\"\"";
        }

        return string.Join(" + ", parts);
    }

    private static string ExpressionFor(string name, IReadOnlyList<KeyValuePair<string, string>> routeExpressions)
    {
        return routeExpressions.First(routeExpression => string.Equals(routeExpression.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static List<PathPiece> Pieces(string path)
    {
        var pieces = new List<PathPiece>();
        var position = 0;
        while (position < path.Length)
        {
            var open = path.IndexOf('{', position);
            var close = open < 0 ? -1 : path.IndexOf('}', open);
            if (open < 0 || close < 0)
            {
                pieces.Add(new PathPiece { Text = path.Substring(position), IsPlaceholder = false });
                break;
            }

            if (open > position)
            {
                pieces.Add(new PathPiece { Text = path.Substring(position, open - position), IsPlaceholder = false });
            }

            pieces.Add(new PathPiece { Text = PlaceholderName(path.Substring(open + 1, close - open - 1)), IsPlaceholder = true });
            position = close + 1;
        }

        return pieces;
    }

    private static string PlaceholderName(string placeholder)
    {
        var name = placeholder.TrimStart('*');
        var nameEnd = name.IndexOfAny(NameEnds);
        return nameEnd < 0 ? name : name.Substring(0, nameEnd);
    }

    private sealed record PathPiece
    {
        public required string Text { get; init; }

        public required bool IsPlaceholder { get; init; }
    }
}
