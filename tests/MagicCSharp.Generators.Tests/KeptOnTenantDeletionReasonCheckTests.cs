using MagicCSharp.Data.EntityFramework.Dals;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Tests;

public class KeptOnTenantDeletionReasonCheckTests
{
    [Fact]
    public void KeptOnTenantDeletion_ReportsMCS1601_ForABlankReason()
    {
        // Arrange: one Dal states its reason, one does not.
        const string source = """
                              using MagicCSharp.Data.EntityFramework.Dals;

                              namespace Acme.Leasing.Data;

                              [KeptOnTenantDeletion("Ledger entries are kept for the tax years after the tenant leaves")]
                              public class LedgerEntryDal
                              {
                              }

                              [KeptOnTenantDeletion(" ")]
                              public class AuditEntryDal
                              {
                              }
                              """;

        // Act
        var generatorRunResult = GeneratorHarness.Run(new GeneratorRunRequest
        {
            SourceFiles = [GeneratorHarness.Subject(source)],
            AdditionalReferences = [MetadataReference.CreateFromFile(typeof(KeptOnTenantDeletionAttribute).Assembly.Location)],
            OtherGenerators = [new KeptOnTenantDeletionReasonCheck()],
        });

        // Assert
        Assert.Empty(generatorRunResult.GeneratedFiles);
        var diagnostic = Assert.Single(generatorRunResult.Diagnostics);
        Assert.Equal("MCS1601 Subject.cs:10 'AuditEntryDal' is [KeptOnTenantDeletion] without a reason; say why its rows outlive their tenant", diagnostic);
    }
}
