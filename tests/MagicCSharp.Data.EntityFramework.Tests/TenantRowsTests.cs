using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.EntityFramework.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>Which rows are a tenant's: narrowing a query to them, and deleting them through a key-keyed repository.</summary>
public class TenantRowsTests : RepositoryTestBase
{
    private ReceiptsRepository Receipts => new ReceiptsRepository(new RepositoryServices<TestDbContext>(DbContextFactory, KeyGen, TimeProvider, NullLoggerFactory.Instance));

    [Fact]
    public async Task KeyRepository_DestroyTenant_DeletesOnlyThatTenantsRows()
    {
        // Arrange
        await Receipts.Create(new ReceiptEdit { TenantId = 7, Text = "Rent March" });
        await Receipts.Create(new ReceiptEdit { TenantId = 7, Text = "Rent April" });
        var otherTenantsReceipt = await Receipts.Create(new ReceiptEdit { TenantId = 8, Text = "Rent March" });

        // Act
        var destroyedCount = await Receipts.DestroyTenant(7);

        // Assert
        Assert.Equal(2, destroyedCount);
        var remaining = Assert.Single(await Receipts.Get(new ReceiptFilter()));
        Assert.Equal(otherTenantsReceipt.Key, remaining.Key);
    }

    [Fact]
    public async Task WhereTenant_KeepsOnlyTheTenantsRows_WhenTheTenantKeyIsNullable()
    {
        // Arrange
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Visits.AddRange(new VisitDal { Id = 1, OwnerId = 7 }, new VisitDal { Id = 2, OwnerId = 8 }, new VisitDal { Id = 3, OwnerId = null });
        await context.SaveChangesAsync();

        // Act
        var visitIds = await TenantRows.WhereTenant(context.Visits, nameof(VisitDal.OwnerId), 7).Select(visit => visit.Id).ToListAsync();

        // Assert
        Assert.Equal([1L], visitIds);
    }
}
