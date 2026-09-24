namespace MagicCSharp.Analyzers.Tests;

public class FileScopedNamespaceAnalyzerTests
{
    [Fact]
    public async Task A_block_namespace_is_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new FileScopedNamespaceAnalyzer(), """
                                                                                           namespace Subject.Leases
                                                                                           {
                                                                                               public class Lease
                                                                                               {
                                                                                               }
                                                                                           }
                                                                                           """);

        Assert.Equal(["MCS0018 Subject.cs:1 Declare namespace 'Subject.Leases' file-scoped, as 'namespace Subject.Leases;'"], diagnostics);
    }

    [Fact]
    public async Task A_file_scoped_namespace_is_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new FileScopedNamespaceAnalyzer(), """
                                                                                           namespace Subject.Leases;

                                                                                           public class Lease
                                                                                           {
                                                                                           }
                                                                                           """);

        Assert.Empty(diagnostics);
    }
}
