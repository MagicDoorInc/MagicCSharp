namespace MagicCSharp.Analyzers.Tests;

public class PatternSugarAnalyzerTests
{
    [Fact]
    public async Task Null_patterns_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new PatternSugarAnalyzer(), """
                                                                                    namespace Subject;

                                                                                    public class LeaseChecks
                                                                                    {
                                                                                        public bool IsMissing(object? lease)
                                                                                        {
                                                                                            return lease is null;
                                                                                        }

                                                                                        public bool IsPresent(object? lease)
                                                                                        {
                                                                                            return lease is not null;
                                                                                        }
                                                                                    }
                                                                                    """);

        Assert.Equal([
            "MCS0003 Subject.cs:7 Replace 'is null' with '== null'",
            "MCS0003 Subject.cs:12 Replace 'is not null' with '!= null'",
        ], diagnostics);
    }

    [Fact]
    public async Task Equality_checks_and_null_switch_arms_are_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new PatternSugarAnalyzer(), """
                                                                                    namespace Subject;

                                                                                    public class LeaseChecks
                                                                                    {
                                                                                        public bool IsMissing(object? lease)
                                                                                        {
                                                                                            return lease == null;
                                                                                        }

                                                                                        public string Describe(string? name)
                                                                                        {
                                                                                            return name switch
                                                                                            {
                                                                                                null => "none",
                                                                                                _ => name,
                                                                                            };
                                                                                        }

                                                                                        public bool IsLease(object value)
                                                                                        {
                                                                                            return value is string;
                                                                                        }
                                                                                    }
                                                                                    """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Property_and_declaration_patterns_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new PatternSugarAnalyzer(), """
                                                                                    namespace Subject;

                                                                                    public class LeaseChecks
                                                                                    {
                                                                                        public int Length(object value)
                                                                                        {
                                                                                            if (value is string { Length: > 0 })
                                                                                            {
                                                                                                return 1;
                                                                                            }

                                                                                            if (value is string text)
                                                                                            {
                                                                                                return text.Length;
                                                                                            }

                                                                                            return 0;
                                                                                        }
                                                                                    }
                                                                                    """);

        Assert.Equal([
            "MCS0004 Subject.cs:7 Property patterns are not allowed; write the checks out explicitly",
            "MCS0005 Subject.cs:12 Pattern declares 'text' inline; use 'as' plus a null check, or an explicit cast",
        ], diagnostics);
    }
}
