namespace MagicCSharp.Analyzers.Tests;

public class FileNameMatchesTypeAnalyzerTests
{
    [Fact]
    public async Task A_file_named_differently_from_its_type_is_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new FileNameMatchesTypeAnalyzer(), [
            new TestSourceFile
            {
                Path = "Leases.cs",
                Text = """
                       namespace Subject;

                       public class Lease
                       {
                       }
                       """,
            },
        ]);

        Assert.Equal(["MCS0014 Leases.cs:3 File 'Leases.cs' declares 'Lease'; rename the file or the type so they match"], diagnostics);
    }

    [Fact]
    public async Task Matching_Program_static_and_enum_only_files_are_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new FileNameMatchesTypeAnalyzer(), [
            new TestSourceFile { Path = "Lease.cs", Text = "namespace Subject;\n\npublic class Lease\n{\n}\n" },
            new TestSourceFile { Path = "LeaseMath.cs", Text = "namespace Subject;\n\npublic static class LeaseRounding\n{\n}\n" },
            new TestSourceFile { Path = "LeaseStates.cs", Text = "namespace Subject;\n\npublic enum LeaseState\n{\n    Active,\n}\n" },
            new TestSourceFile { Path = "Program.cs", Text = "namespace Subject;\n\npublic class LeasingProgram\n{\n}\n" },
        ]);

        Assert.Empty(diagnostics);
    }
}
