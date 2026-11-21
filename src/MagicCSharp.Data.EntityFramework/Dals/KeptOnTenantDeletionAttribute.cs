namespace MagicCSharp.Data.EntityFramework.Dals;

/// <summary>
///     Tenant deletion leaves this Dal's table alone, for the reason given.
///     <para>
///         Registration by convention makes every tenant-scoped repository a destroyer, so a table that must outlive
///         its tenant — an audit trail, a ledger kept for the tax years — says so here, where the next reader sees
///         why. The reason is required, and the build refuses a blank one (MCS1601).
///     </para>
/// </summary>
/// <param name="reason">Why the rows stay when their tenant is deleted.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class KeptOnTenantDeletionAttribute(string reason) : Attribute
{
    /// <summary>Why the rows stay when their tenant is deleted.</summary>
    public string Reason { get; } = reason;
}
