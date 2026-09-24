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
        var content = Module("        services.AddPostgresDbContextFactory<C>(configuration);");

        var result = SourceEdits.InsertBefore(content, "return services;", "services.AddScoped<A, B>();");

        Assert.Contains("services.AddScoped<A, B>();", result);
        Assert.True(result.IndexOf("AddScoped", StringComparison.Ordinal)
                    < result.IndexOf("return services;", StringComparison.Ordinal));
    }

    [Fact]
    public void InsertBefore_indents_to_match_the_anchor()
    {
        // The line used to land at sixteen spaces: it was spliced in after the anchor's own indentation,
        // so the caller's eight were added to the anchor's eight. Every generated repositories module had
        // it, and it is the first generated code anyone reads.
        var content = Module("        services.AddPostgresDbContextFactory<C>(configuration);");

        var result = SourceEdits.InsertBefore(content, "return services;", "services.AddScoped<A, B>();");

        Assert.Contains("\n        services.AddScoped<A, B>();\n", result);
        Assert.DoesNotContain("                services.AddScoped", result);
    }

    [Fact]
    public void InsertBefore_keeps_the_blank_line_above_the_anchor()
    {
        var content = Module("        services.AddPostgresDbContextFactory<C>(configuration);");

        var result = SourceEdits.InsertBefore(content, "return services;", "services.AddScoped<A, B>();");

        Assert.Contains("services.AddScoped<A, B>();\n\n        return services;", result);
    }

    [Fact]
    public void InsertBefore_groups_successive_statements()
    {
        // Two entities scaffolded one after the other belong next to each other, not with a blank line
        // between every pair.
        var content = Module("        services.AddPostgresDbContextFactory<C>(configuration);");

        var result = SourceEdits.InsertBefore(content, "return services;", "services.AddScoped<A, B>();");
        result = SourceEdits.InsertBefore(result, "return services;", "services.AddScoped<C, D>();");

        Assert.Contains("        services.AddScoped<A, B>();\n        services.AddScoped<C, D>();\n", result);
    }

    [Fact]
    public void InsertBefore_joins_the_statements_already_there()
    {
        // The new registration belongs with the existing ones, not in the gap that separates them from
        // the return.
        var content = Module("        services.AddPostgresDbContextFactory<C>(configuration);");

        var result = SourceEdits.InsertBefore(content, "return services;", "services.AddScoped<A, B>();");

        Assert.Contains(
            "        services.AddPostgresDbContextFactory<C>(configuration);\n        services.AddScoped<A, B>();\n\n        return services;",
            result);
    }

    /// <summary>A registration method shaped like the one the CLI actually edits.</summary>
    private static string Module(string body)
    {
        return "public static class M\n{\n    public static IServiceCollection Add(this IServiceCollection services)\n    {\n"
               + body + "\n\n        return services;\n    }\n}\n";
    }

    [Fact]
    public void InsertBefore_says_which_anchor_was_missing()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SourceEdits.InsertBefore("nothing here", "return services;", "x"));

        Assert.Contains("return services;", exception.Message);
    }
}
