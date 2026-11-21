namespace MagicCSharp.Data.EntityFramework.Dals;

/// <summary>
///     Names the DAL property that says which tenant a row belongs to, once for every DAL in the assembly:
///     <c>[assembly: TenantKey("CompanyId")]</c>.
///     <para>
///         Two things follow from it. A DAL with that property, of type <c>long</c> or <c>long?</c>, is tenant-scoped:
///         its repository's <c>DestroyTenant</c> deletes its rows by it. And the generated <c>Apply</c> never copies
///         it, because a row does not move to another tenant; the generated <c>From</c> sets it once.
///     </para>
/// </summary>
/// <param name="propertyName">The DAL property holding the tenant id.</param>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class TenantKeyAttribute(string propertyName) : Attribute
{
    /// <summary>The DAL property holding the tenant id.</summary>
    public string PropertyName { get; } = propertyName;
}
