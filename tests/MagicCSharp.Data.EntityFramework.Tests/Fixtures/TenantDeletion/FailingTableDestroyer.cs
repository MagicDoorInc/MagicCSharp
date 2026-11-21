using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Repositories;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>A destroyer whose delete fails, as one stopped by a key or a lost connection does.</summary>
public class FailingTableDestroyer(Type dalType) : ITenantTableDestroyer
{
    public Type DbContextType
    {
        get
        {
            return typeof(TenantDeletionDbContext);
        }
    }

    public Type DalType
    {
        get
        {
            return dalType;
        }
    }

    public Task<int> DestroyTenant(long tenantId)
    {
        throw new InvalidOperationException("The meter service did not answer.");
    }
}
