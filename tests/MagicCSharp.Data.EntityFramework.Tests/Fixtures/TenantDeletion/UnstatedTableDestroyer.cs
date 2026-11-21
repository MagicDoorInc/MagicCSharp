using MagicCSharp.Data.Repositories;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>A destroyer that does not say which table it empties.</summary>
public class UnstatedTableDestroyer : ITenantScopedDestroyer
{
    public Task<int> DestroyTenant(long tenantId)
    {
        return Task.FromResult(0);
    }
}
