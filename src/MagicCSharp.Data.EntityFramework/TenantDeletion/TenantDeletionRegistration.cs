using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MagicCSharp.Data.EntityFramework.TenantDeletion;

public static class TenantDeletionRegistration
{
    /// <summary>
    ///     Registers <see cref="TenantDeletionPlanner" /> and <see cref="TenantDataDestroyer" />, scoped, unless already
    ///     registered. <c>AddRepositoriesFrom&lt;TContext&gt;()</c> calls it.
    /// </summary>
    public static IServiceCollection AddTenantDeletion(this IServiceCollection services)
    {
        services.TryAddScoped<TenantDeletionPlanner>();
        services.TryAddScoped<TenantDataDestroyer>();
        return services;
    }
}
