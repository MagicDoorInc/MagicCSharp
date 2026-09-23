namespace MagicCSharp.Analyzers.Tests;

public class DbSetPluralAnalyzerTests
{
    [Fact]
    public async Task A_singular_DbSet_is_reported_and_a_plural_one_is_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new DbSetPluralAnalyzer(), """
                                                                                   using Microsoft.EntityFrameworkCore;

                                                                                   namespace Subject;

                                                                                   public class LeaseDal
                                                                                   {
                                                                                   }

                                                                                   public class LeasingContext
                                                                                   {
                                                                                       public DbSet<LeaseDal> Lease { get; set; } = null!;

                                                                                       public DbSet<LeaseDal> Leases { get; set; } = null!;

                                                                                       public DbSet<LeaseDal> SalesPeople { get; set; } = null!;
                                                                                   }
                                                                                   """);

        Assert.Equal(["MCS0021 Subject.cs:11 DbSet property 'Lease' must be plural"], diagnostics);
    }
}
