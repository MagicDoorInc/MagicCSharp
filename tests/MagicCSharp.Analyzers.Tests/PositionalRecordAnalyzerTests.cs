namespace MagicCSharp.Analyzers.Tests;

public class PositionalRecordAnalyzerTests
{
    [Fact]
    public async Task A_positional_record_is_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new PositionalRecordAnalyzer(), """
                                                                                        namespace Subject;

                                                                                        public record LeaseSummary(long LeaseId, string Name);

                                                                                        public record struct LeasePeriod(int Months);
                                                                                        """);

        Assert.Equal([
            "MCS0001 Subject.cs:3 Record 'LeaseSummary' uses a positional parameter list; declare its members as 'public required T Name { get; init; }' in a body instead",
            "MCS0001 Subject.cs:5 Record 'LeasePeriod' uses a positional parameter list; declare its members as 'public required T Name { get; init; }' in a body instead",
        ], diagnostics);
    }

    [Fact]
    public async Task A_record_with_a_body_or_an_empty_parameter_list_is_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new PositionalRecordAnalyzer(), """
                                                                                        namespace Subject;

                                                                                        public record LeaseSummary
                                                                                        {
                                                                                            public required long LeaseId { get; init; }
                                                                                        }

                                                                                        public record EmptyMarker();
                                                                                        """);

        Assert.Empty(diagnostics);
    }
}
