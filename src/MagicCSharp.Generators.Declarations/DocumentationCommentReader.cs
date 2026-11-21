using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Reads the doc comment of a symbol.</summary>
public static class DocumentationCommentReader
{
    /// <summary>
    ///     The doc comment of a symbol. In source it is read from the <c>///</c> lines themselves, which are there
    ///     whether or not the project generates a documentation file; from another assembly it comes from that
    ///     assembly's XML documentation file, when the compiler found one.
    /// </summary>
    public static DocumentationComment Read(ISymbol symbol, CancellationToken cancellationToken)
    {
        if (symbol.DeclaringSyntaxReferences.Length > 0)
        {
            return ReadFromSource(symbol, cancellationToken);
        }

        return ReadFromXml(symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken) ?? "");
    }

    private static DocumentationComment ReadFromSource(ISymbol symbol, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            var leadingTrivia = syntaxReference.GetSyntax(cancellationToken).GetLeadingTrivia().ToFullString();
            foreach (var line in leadingTrivia.Split('\n'))
            {
                var trimmedLine = line.Trim('\r').TrimStart();
                if (trimmedLine.StartsWith("///", StringComparison.Ordinal))
                {
                    lines.Add(trimmedLine.Substring(3).TrimEnd());
                }
            }
        }

        return new DocumentationComment { Lines = new EquatableArray<string>(lines) };
    }

    private static DocumentationComment ReadFromXml(string xml)
    {
        // <member name="M:…">, the comment's lines indented under it, then </member>.
        var lines = xml.Replace("\r", "").Split('\n').ToList();
        var innerLines = lines.Where(line => !line.TrimStart().StartsWith("<member ", StringComparison.Ordinal) &&
                                             line.Trim() != "</member>" && line.Trim() != "")
            .ToArray();
        if (innerLines.Length == 0)
        {
            return DocumentationComment.Empty;
        }

        var indentation = innerLines.Min(line => line.Length - line.TrimStart().Length);
        return new DocumentationComment
        {
            Lines = new EquatableArray<string>(innerLines.Select(line => " " + line.Substring(indentation).TrimEnd())),
        };
    }
}
