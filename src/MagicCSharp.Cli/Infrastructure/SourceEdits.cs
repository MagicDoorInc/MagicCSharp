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

    /// <summary>Inserts a line immediately before an anchor, preserving the anchor's indentation.</summary>
    public static string InsertBefore(string content, string anchor, string line)
    {
        var index = content.IndexOf(anchor, StringComparison.Ordinal);

        if (index < 0)
        {
            throw new InvalidOperationException($"Anchor not found: {anchor}");
        }

        var newLine = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        return content[..index] + line + newLine + newLine + "        " + content[index..];
    }
}
