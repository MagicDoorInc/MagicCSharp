namespace MagicCSharp.Analyzers.Tests;

public class GroupedTypeFileAnalyzerTests
{
    [Fact]
    public async Task A_use_case_file_with_its_interface_models_and_enum_is_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new GroupedTypeFileAnalyzer(), [
            new TestSourceFile
            {
                Path = "ApproveLeaseUseCase.cs",
                Text = """
                       namespace Subject;

                       public interface IApproveLeaseUseCase
                       {
                           ApproveLeaseResult Execute(ApproveLeaseRequest request);
                       }

                       public record ApproveLeaseRequest
                       {
                           public required ApprovalKind Kind { get; init; }
                       }

                       public record ApproveLeaseResult
                       {
                           public required bool IsApproved { get; init; }
                       }

                       public record ApproveLeaseResponse
                       {
                       }

                       public enum ApprovalKind
                       {
                           Manual,
                       }

                       public class ApproveLeaseUseCase : IApproveLeaseUseCase
                       {
                           public ApproveLeaseResult Execute(ApproveLeaseRequest request)
                           {
                               return new ApproveLeaseResult { IsApproved = true };
                           }
                       }
                       """,
            },
        ]);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task An_unrelated_public_type_is_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new GroupedTypeFileAnalyzer(), [
            new TestSourceFile
            {
                Path = "Lease.cs",
                Text = """
                       namespace Subject;

                       public class Lease
                       {
                       }

                       public class Tenant
                       {
                       }
                       """,
            },
        ]);

        Assert.Equal(["MCS0013 Lease.cs:7 'Lease' does not support 'Tenant'; move 'Tenant' to Tenant.cs"], diagnostics);
    }

    [Fact]
    public async Task Generic_and_non_generic_types_named_for_the_file_are_both_primary()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new GroupedTypeFileAnalyzer(), [
            new TestSourceFile
            {
                Path = "ComparableRange.cs",
                Text = """
                       namespace Subject;

                       public record ComparableRange<T>
                       {
                       }

                       public static class ComparableRange
                       {
                       }
                       """,
            },
        ]);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task A_file_without_a_primary_type_is_reported_as_a_junk_drawer()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new GroupedTypeFileAnalyzer(), [
            new TestSourceFile
            {
                Path = "BankFeedEnums.cs",
                Text = """
                       namespace Subject;

                       public enum BankFeedEntryStatus
                       {
                           Pending,
                       }

                       public enum BankFeedSyncStatus
                       {
                           Running,
                       }
                       """,
            },
        ]);

        Assert.Equal(["MCS0013 BankFeedEnums.cs:8 File 'BankFeedEnums.cs' groups unrelated types; 'BankFeedSyncStatus' belongs in its own file named for it"],
            diagnostics);
    }
}
