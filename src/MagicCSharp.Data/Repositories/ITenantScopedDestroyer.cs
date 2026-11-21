namespace MagicCSharp.Data.Repositories;

/// <summary>
///     Deletes every row one tenant owns in one table, for when the tenant itself is deleted.
///     <para>
///         The repository bases implement it: a row belongs to a tenant through the property its DAL assembly names
///         with <c>[assembly: TenantKey("…")]</c>. <c>AddRepositoriesFrom&lt;TContext&gt;()</c> registers every
///         implementation, so the handler that deletes a tenant resolves them all as
///         <c>IEnumerable&lt;ITenantScopedDestroyer&gt;</c>.
///     </para>
/// </summary>
public interface ITenantScopedDestroyer
{
    /// <summary>
    ///     Removes every row of the tenant for good, soft-deleted rows included, and returns how many went.
    /// </summary>
    /// <param name="tenantId">The tenant whose rows are removed.</param>
    Task<int> DestroyTenant(long tenantId);
}
