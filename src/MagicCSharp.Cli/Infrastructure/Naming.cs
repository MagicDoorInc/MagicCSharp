using System.Text.RegularExpressions;

namespace MagicCSharp.Cli.Infrastructure;

/// <summary>Name shapes the generated code depends on agreeing about.</summary>
public static partial class Naming
{
    /// <summary>
    ///     English plural, good enough for the type and table names the generators produce. Wrong for
    ///     irregulars — a "Person" repository comes out as "Persons" — which is a rename away and better
    ///     than carrying a dictionary of exceptions.
    /// </summary>
    public static string Pluralize(string input)
    {
        if (input.EndsWith('y') && input.Length > 1 && !"aeiouAEIOU".Contains(input[^2]))
        {
            return input[..^1] + "ies";
        }

        if (input.EndsWith('s') || input.EndsWith('x') || input.EndsWith('z') ||
            input.EndsWith("ch", StringComparison.Ordinal) || input.EndsWith("sh", StringComparison.Ordinal))
        {
            return input + "es";
        }

        return input + "s";
    }

    /// <summary>PascalCase to plural snake_case: "OrderLine" becomes "order_lines".</summary>
    public static string ToTableName(string input)
    {
        var snake = CapitalLetter().Replace(input, match => "_" + match.Value.ToLowerInvariant()).TrimStart('_');
        return Pluralize(snake);
    }

    /// <summary>A single PascalCase word, no dots.</summary>
    public static bool IsPascalWord(string? value)
    {
        return value != null && PascalWord().IsMatch(value);
    }

    /// <summary>PascalCase segments separated by dots, e.g. "Domains.Orders".</summary>
    public static bool IsDottedPascal(string? value)
    {
        return value != null && DottedPascal().IsMatch(value);
    }

    /// <summary>Lowercase with underscores, e.g. "order_management".</summary>
    public static bool IsDatabaseName(string? value)
    {
        return value != null && DatabaseName().IsMatch(value);
    }

    [GeneratedRegex("([A-Z])")]
    private static partial Regex CapitalLetter();

    [GeneratedRegex("^[A-Z][0-9a-zA-Z]*$")]
    private static partial Regex PascalWord();

    [GeneratedRegex(@"^[A-Z][0-9a-zA-Z]*(\.[A-Z][0-9a-zA-Z]*)*$")]
    private static partial Regex DottedPascal();

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex DatabaseName();
}
