namespace MagicCSharp.Analyzers.Tests;

public class CrypticAbbreviationAnalyzerTests
{
    [Fact]
    public async Task Abbreviations_in_type_member_parameter_and_local_names_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new CrypticAbbreviationAnalyzer(), """
                                                                                           namespace Subject;

                                                                                           public class LeaseCtx
                                                                                           {
                                                                                               public decimal RetailPriceVal { get; init; }

                                                                                               public void Apply(object leaseRepo)
                                                                                               {
                                                                                                   var tmp = 1;
                                                                                               }
                                                                                           }
                                                                                           """);

        Assert.Equal([
            "MCS0010 Subject.cs:3 Identifier 'LeaseCtx' contains the abbreviation 'Ctx'; spell the word out in full",
            "MCS0010 Subject.cs:5 Identifier 'RetailPriceVal' contains the abbreviation 'Val'; spell the word out in full",
            "MCS0010 Subject.cs:7 Identifier 'leaseRepo' contains the abbreviation 'Repo'; spell the word out in full",
            "MCS0010 Subject.cs:9 Identifier 'tmp' contains the abbreviation 'tmp'; spell the word out in full",
        ], diagnostics);
    }

    [Fact]
    public async Task Obsolete_members_and_spelled_out_words_are_skipped()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new CrypticAbbreviationAnalyzer(), """
                                                                                           namespace Subject;

                                                                                           public class LeaseFees
                                                                                           {
                                                                                               public decimal RetailPriceValue { get; init; }

                                                                                               [System.Obsolete("Use RetailPriceValue")]
                                                                                               public decimal RetailPriceVal
                                                                                               {
                                                                                                   get => RetailPriceValue;
                                                                                                   init => RetailPriceValue = value;
                                                                                               }

                                                                                               public long LeaseId { get; init; }

                                                                                               public string ComputeMd5Hash(string leaseDto, string response)
                                                                                               {
                                                                                                   return leaseDto + response;
                                                                                               }
                                                                                           }
                                                                                           """);

        Assert.Empty(diagnostics);
    }
}
