namespace MagicCSharp.Analyzers.Tests;

public class StatementBodyMethodAnalyzerTests
{
    [Fact]
    public async Task An_expression_bodied_method_and_local_function_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new StatementBodyMethodAnalyzer(), """
                                                                                           namespace Subject;

                                                                                           public class LeaseTotals
                                                                                           {
                                                                                               public int Total() => 1;

                                                                                               public int Double()
                                                                                               {
                                                                                                   return Twice(1);

                                                                                                   int Twice(int value) => value * 2;
                                                                                               }
                                                                                           }
                                                                                           """);

        Assert.Equal([
            "MCS0009 Subject.cs:5 'Total' is expression-bodied; write it with a block body",
            "MCS0009 Subject.cs:11 'Twice' is expression-bodied; write it with a block body",
        ], diagnostics);
    }

    [Fact]
    public async Task An_expression_bodied_property_is_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new StatementBodyMethodAnalyzer(), """
                                                                                           namespace Subject;

                                                                                           public class LeaseTotals
                                                                                           {
                                                                                               public int Total => 1;
                                                                                           }
                                                                                           """);

        Assert.Empty(diagnostics);
    }
}
