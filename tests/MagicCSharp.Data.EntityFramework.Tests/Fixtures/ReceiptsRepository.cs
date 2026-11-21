using MagicCSharp.Data.EntityFramework.Repositories;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>A key-keyed repository, whose <c>DestroyTenant</c> is the one from the base.</summary>
public class ReceiptsRepository(RepositoryServices<TestDbContext> repositoryServices)
    : BaseKeyRepository<TestDbContext, ReceiptDal, Receipt, ReceiptFilter, ReceiptEdit>(repositoryServices)
{
    protected override ReceiptDal CreateDal(ReceiptEdit edit)
    {
        return new ReceiptDal { Key = KeyGenService.GetKey(), TenantId = edit.TenantId, Text = edit.Text };
    }

    protected override IQueryable<ReceiptDal> ApplyFilter(IQueryable<ReceiptDal> query, ReceiptFilter filter)
    {
        return filter.TenantId == null ? query : query.Where(receipt => receipt.TenantId == filter.TenantId);
    }
}
