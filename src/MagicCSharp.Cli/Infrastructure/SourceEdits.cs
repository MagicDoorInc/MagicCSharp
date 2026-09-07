namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     Small, targeted edits to existing C# files — adding a using, inserting a member. Text manipulation
///     rather than Roslyn, because the edits are narrow and the files are ones these commands generated.
/// </summary>
public static class SourceEdits
{
    /// <summary>
    ///     Adds a using directive unless it is already there, keeping the block sorted so the result looks
    ///     hand-written rather than appended to.
    /// </summary>
    public static string EnsureUsing(string content, string namespaceName)
    {
        var directive = $"using {namespaceName};";

        if (content.Contains(directive, StringComparison.Ordinal))
        {
            return content;
        }

        var newLine = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = content.Split(newLine).ToList();
        var lastUsing = lines.FindLastIndex(line => line.StartsWith("using ", StringComparison.Ordinal));

        if (lastUsing < 0)
        {
            lines.Insert(0, "");
            lines.Insert(0, directive);
            return string.Join(newLine, lines);
        }

        var insertAt = lines.FindIndex(0, lastUsing + 1, line =>
            line.StartsWith("using ", StringComparison.Ordinal) && string.CompareOrdinal(line, directive) > 0);

        lines.Insert(insertAt < 0 ? lastUsing + 1 : insertAt, directive);

        return string.Join(newLine, lines);
    }

    /// <summary>
    ///     Inserts a member before the closing brace of the last type in the file.
    /// </summary>
    public static string InsertBeforeLastBrace(string content, string member)
    {
        var lastBrace = content.LastIndexOf('}');

        if (lastBrace < 0)
        {
            throw new InvalidOperationException("No closing brace to insert before.");
        }

        var newLine = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        return content[..lastBrace].TrimEnd() + newLine + newLine + member + newLine + content[lastBrace..];
    }

    /// <summary>
    ///     Inserts a statement above the anchor's line, indented to match it.
    ///     <para>
    ///         The line lands after the last statement rather than immediately above the anchor, so blank
    ///         lines separating the anchor from the body — the one before <c>return services;</c>, say —
    ///         stay where the author put them and successive insertions group together.
    ///     </para>
    ///     <para>
    ///         This used to splice at the anchor itself, which put the new line after the anchor's own
    ///         leading whitespace: the caller's eight spaces became sixteen, and the anchor was pushed onto
    ///         a fresh line of stray spaces. Every generated repositories module carried it.
    ///     </para>
    /// </summary>
    /// <param name="content">The file.</param>
    /// <param name="anchor">Text on the line to insert above, e.g. <c>return services;</c>.</param>
    /// <param name="line">The statement, without indentation — the anchor's is applied.</param>
    public static string InsertBefore(string content, string anchor, string line)
    {
        var index = content.IndexOf(anchor, StringComparison.Ordinal);

        if (index < 0)
        {
            throw new InvalidOperationException($"Anchor not found: {anchor}");
        }

        var newLine = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var anchorLineStart = content.LastIndexOf('\n', index) + 1;
        var indent = content[anchorLineStart..index];

        // Back up over the blank lines above the anchor, so the statement joins the ones already there
        // instead of landing in the gap that separates them from it.
        var insertAt = content[..anchorLineStart].TrimEnd().Length;

        if (insertAt == 0)
        {
            insertAt = anchorLineStart;
        }
        else
        {
            insertAt += newLine.Length;
        }

        return content[..insertAt] + indent + line.Trim() + newLine + content[insertAt..];
    }
}
