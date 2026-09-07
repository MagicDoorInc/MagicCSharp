using MagicCSharp.Data.Utils;
using Xunit;

namespace MagicCSharp.Tests;

public class SearchTextTests
{
    [Fact]
    public void Normalize_lowercases_and_drops_punctuation()
    {
        Assert.Equal("acme corp", SearchText.Normalize(["Acme, Corp."]));
    }

    [Fact]
    public void Normalize_splits_hyphenated_words()
    {
        Assert.Equal("well known name", SearchText.Normalize(["well-known name"]));
    }

    [Fact]
    public void Normalize_drops_repeats_because_the_column_is_an_index_not_a_record()
    {
        Assert.Equal("smith jane", SearchText.Normalize(["Smith Jane", "smith"]));
    }

    [Fact]
    public void Normalize_joins_several_terms()
    {
        Assert.Equal("jane smith apartment 4b", SearchText.Normalize(["Jane Smith", "Apartment 4B"]));
    }

    [Fact]
    public void Normalize_truncates_so_one_row_cannot_grow_without_bound()
    {
        var terms = Enumerable.Range(0, 500).Select(i => $"word{i}").ToList();

        Assert.True(SearchText.Normalize(terms).Length <= 1000);
    }

    [Fact]
    public void Synonyms_rewrite_both_sides_so_a_short_query_finds_a_long_value()
    {
        var synonyms = new Dictionary<string, string> { ["street"] = "st" };

        var stored = SearchText.Normalize(["12 Baker Street"], synonyms);
        var query = SearchText.SplitQuery("baker st", synonyms);

        Assert.Equal("12 baker st", stored);
        Assert.All(query, word => Assert.Contains(word, stored));
    }

    [Fact]
    public void SplitQuery_returns_each_word_so_all_of_them_must_match()
    {
        Assert.Equal(["jane", "smith"], SearchText.SplitQuery("Jane  Smith!"));
    }

    [Fact]
    public void SplitQuery_of_blank_input_is_empty()
    {
        Assert.Empty(SearchText.SplitQuery("   "));
    }

    [Fact]
    public void Digits_survive_normalization()
    {
        Assert.Equal("unit 4b 2026", SearchText.Normalize(["Unit 4B (2026)"]));
    }

    [Fact]
    public void Non_ascii_letters_survive()
    {
        // "Søgaard" used to index as "sgaard", so nobody searching for the actual name ever found it.
        Assert.Equal("søgaard kasper", SearchText.Normalize(["Søgaard, Kasper"]));
    }

    [Fact]
    public void Non_latin_scripts_survive_too()
    {
        Assert.Equal("東京 tokyo", SearchText.Normalize(["東京 (Tokyo)"]));
    }
}
