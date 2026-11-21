using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Repositories;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>A destroyer that names its table and deletes nothing: enough to be ordered.</summary>
public class StatedTableDestroyer(Type dalType) : ITenantTableDestroyer
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
        return Task.FromResult(0);
    }
}
