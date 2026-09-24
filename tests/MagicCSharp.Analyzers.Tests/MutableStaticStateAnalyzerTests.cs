namespace MagicCSharp.Analyzers.Tests;

public class MutableStaticStateAnalyzerTests
{
    [Fact]
    public async Task A_mutable_static_field_and_a_settable_static_property_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new MutableStaticStateAnalyzer(), """
                                                                                          namespace Subject;

                                                                                          public class LeaseCounter
                                                                                          {
                                                                                              private static int nextLeaseId;

                                                                                              public static string Prefix { get; set; } = "";
                                                                                          }
                                                                                          """);

        Assert.Equal([
            "MCS0017 Subject.cs:5 'nextLeaseId' is a mutable static field; make it an instance member, or 'readonly' or 'const' if it never changes",
            "MCS0017 Subject.cs:7 'Prefix' is a mutable static property; make it an instance member, or 'readonly' or 'const' if it never changes",
        ], diagnostics);
    }

    [Fact]
    public async Task Readonly_const_getter_only_and_ThreadStatic_members_are_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new MutableStaticStateAnalyzer(), """
                                                                                          namespace Subject;

                                                                                          public class LeaseCounter
                                                                                          {
                                                                                              private const int MaxLeases = 10;

                                                                                              private static readonly string[] Prefixes = { "L" };

                                                                                              [System.ThreadStatic]
                                                                                              private static int currentLeaseId;

                                                                                              public static string Prefix => Prefixes[0];
                                                                                          }
                                                                                          """);

        Assert.Empty(diagnostics);
    }
}
