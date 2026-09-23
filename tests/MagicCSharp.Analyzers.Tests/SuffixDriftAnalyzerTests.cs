namespace MagicCSharp.Analyzers.Tests;

public class SuffixDriftAnalyzerTests
{
    [Fact]
    public async Task Use_cases_and_repositories_without_their_suffix_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new SuffixDriftAnalyzer(), """
                                                                                   using MagicCSharp.UseCases;

                                                                                   namespace Subject;

                                                                                   public interface ILeaseValidator : IMagicUseCase
                                                                                   {
                                                                                   }

                                                                                   public class LeaseValidator : ILeaseValidator
                                                                                   {
                                                                                   }

                                                                                   public interface ILeasesRepository
                                                                                   {
                                                                                   }

                                                                                   public class LeasesStore : ILeasesRepository
                                                                                   {
                                                                                   }
                                                                                   """);

        Assert.Equal([
            "MCS0012 Subject.cs:5 Type 'ILeaseValidator' implements IMagicUseCase but its name does not end in 'UseCase'",
            "MCS0012 Subject.cs:9 Type 'LeaseValidator' implements IMagicUseCase but its name does not end in 'UseCase'",
            "MCS0012 Subject.cs:17 Type 'LeasesStore' implements ILeasesRepository but its name does not end in 'Repository'",
        ], diagnostics);
    }

    [Fact]
    public async Task Suffixed_types_and_a_lookalike_use_case_interface_are_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new SuffixDriftAnalyzer(), """
                                                                                   namespace Subject;

                                                                                   public interface IMagicUseCase
                                                                                   {
                                                                                   }

                                                                                   public interface IValidateLeaseUseCase : MagicCSharp.UseCases.IMagicUseCase
                                                                                   {
                                                                                   }

                                                                                   public class ValidateLeaseUseCase : IValidateLeaseUseCase
                                                                                   {
                                                                                   }

                                                                                   public class LeaseValidator : IMagicUseCase
                                                                                   {
                                                                                   }
                                                                                   """);

        Assert.Empty(diagnostics);
    }
}
