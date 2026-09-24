using System.Text.RegularExpressions;

namespace MagicCSharp.Data.Utils;

/// <summary>
///     Normalizes text for the denormalized search column maintained by
///     <see cref="Repositories.ISearchRepository{TKey}" />.
///     <para>
///         Both sides go through the same normalization, which is what makes matching work: the stored column and
///         the incoming query are lowercased, stripped of punctuation, and optionally rewritten through a synonym
///         map, so "St." in the query finds "Street" in the data if the map says they are the same.
///     </para>
///     <para>
///         No synonyms are built in. What counts as a synonym is domain knowledge — street-type abbreviations for
///         addresses, part-number prefixes for a catalogue — so the repository supplies its own map or none.
///     </para>
/// </summary>
public static partial class SearchText
{
    private const int MaxStoredLength = 1000;

    /// <summary>
    ///     Build the value to store for a row, from the terms worth searching it by.
    ///     Duplicates are dropped and the result is truncated, since a search column is an index, not a record.
    /// </summary>
    public static string Normalize(IReadOnlyList<string> terms, IReadOnlyDictionary<string, string>? synonyms = null)
    {
        var normalized = NormalizeWords(string.Join(" ", terms), synonyms).Distinct();

        var joined = string.Join(" ", normalized);

        return joined.Length > MaxStoredLength ? joined[..MaxStoredLength] : joined;
    }

    /// <summary>
    ///     Split an incoming search string into the terms a row must contain. A row matches when it contains
    ///     every one of them, so more words narrow the result rather than widening it.
    /// </summary>
    public static IReadOnlyList<string> SplitQuery(string search, IReadOnlyDictionary<string, string>? synonyms = null)
    {
        return NormalizeWords(search, synonyms).ToList();
    }

    private static IEnumerable<string> NormalizeWords(string input, IReadOnlyDictionary<string, string>? synonyms)
    {
        var cleaned = NonAlphanumeric().Replace(input.Replace("-", " ").ToLowerInvariant(), "");

        foreach (var word in cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return synonyms != null && synonyms.TryGetValue(word, out var synonym) ? synonym : word;
        }
    }

    [GeneratedRegex(@"[^\p{L}\p{N}\s]")]
    private static partial Regex NonAlphanumeric();
}
