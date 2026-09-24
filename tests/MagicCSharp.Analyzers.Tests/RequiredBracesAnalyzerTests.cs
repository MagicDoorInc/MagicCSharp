namespace MagicCSharp.Analyzers.Tests;

public class RequiredBracesAnalyzerTests
{
    [Fact]
    public async Task Braceless_bodies_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new RequiredBracesAnalyzer(), """
                                                                                      namespace Subject;

                                                                                      public class LeaseChecks
                                                                                      {
                                                                                          public int Count(int[] values, bool isEmpty)
                                                                                          {
                                                                                              var total = 0;
                                                                                              if (isEmpty)
                                                                                                  return 0;
                                                                                              else
                                                                                                  total = 1;

                                                                                              foreach (var value in values)
                                                                                                  total += value;

                                                                                              return total;
                                                                                          }
                                                                                      }
                                                                                      """);

        Assert.Equal([
            "MCS0006 Subject.cs:8 The body of this 'if' is not wrapped in braces",
            "MCS0006 Subject.cs:10 The body of this 'else' is not wrapped in braces",
            "MCS0006 Subject.cs:13 The body of this 'foreach' is not wrapped in braces",
        ], diagnostics);
    }

    [Fact]
    public async Task Else_if_chains_and_stacked_usings_are_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new RequiredBracesAnalyzer(), """
                                                                                      namespace Subject;

                                                                                      public class LeaseChecks
                                                                                      {
                                                                                          public int Read(bool isFirst, bool isSecond)
                                                                                          {
                                                                                              if (isFirst)
                                                                                              {
                                                                                                  return 1;
                                                                                              }
                                                                                              else if (isSecond)
                                                                                              {
                                                                                                  return 2;
                                                                                              }

                                                                                              using (var first = new System.IO.MemoryStream())
                                                                                              using (var second = new System.IO.MemoryStream())
                                                                                              {
                                                                                                  return (int)(first.Length + second.Length);
                                                                                              }
                                                                                          }
                                                                                      }
                                                                                      """);

        Assert.Empty(diagnostics);
    }
}
