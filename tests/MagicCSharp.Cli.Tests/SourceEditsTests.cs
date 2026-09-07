using MagicCSharp.Cli.Infrastructure;

namespace MagicCSharp.Cli.Tests;

public class SourceEditsTests
{
    [Fact]
    public void EnsureUsing_adds_it_in_sorted_position()
    {
        var content = "using A.One;\nusing C.Three;\n\nnamespace X;\n";

        var result = SourceEdits.EnsureUsing(content, "B.Two");

        Assert.Equal("using A.One;\nusing B.Two;\nusing C.Three;\n\nnamespace X;\n", result);
    }

    [Fact]
    public void EnsureUsing_does_nothing_when_already_present()
    {
        var content = "using A.One;\n\nnamespace X;\n";

        Assert.Equal(content, SourceEdits.EnsureUsing(content, "A.One"));
    }

    [Fact]
    public void EnsureUsing_handles_a_file_with_no_usings()
    {
        var result = SourceEdits.EnsureUsing("namespace X;\n", "A.One");

        Assert.StartsWith("using A.One;\n", result);
        Assert.Contains("namespace X;", result);
    }

    [Fact]
    public void EnsureUsing_does_not_match_a_longer_namespace_by_prefix()
    {
        // "using A.One;" must not satisfy a request for "A.One.Two".
        var content = "using A.One;\n\nnamespace X;\n";

        var result = SourceEdits.EnsureUsing(content, "A.One.Two");

        Assert.Contains("using A.One.Two;", result);
    }

    [Fact]
    public void InsertBeforeLastBrace_puts_the_member_inside_the_type()
    {
        var content = "namespace X;\n\npublic class C\n{\n}\n";

        var result = SourceEdits.InsertBeforeLastBrace(content, "    public int Value { get; set; }");

        Assert.Contains("    public int Value { get; set; }", result);
        Assert.EndsWith("}\n", result);
        Assert.True(result.IndexOf("public int Value", StringComparison.Ordinal) < result.LastIndexOf('}'));
    }

    [Fact]
    public void InsertBefore_places_the_line_ahead_of_the_anchor()
    {
        var content = "    {\n        return services;\n    }\n";

        var result = SourceEdits.InsertBefore(content, "return services;", "        services.AddScoped<A, B>();");

        Assert.True(result.IndexOf("AddScoped", StringComparison.Ordinal)
                    < result.IndexOf("return services;", StringComparison.Ordinal));
    }

    [Fact]
    public void InsertBefore_says_which_anchor_was_missing()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SourceEdits.InsertBefore("nothing here", "return services;", "x"));

        Assert.Contains("return services;", exception.Message);
    }
}
