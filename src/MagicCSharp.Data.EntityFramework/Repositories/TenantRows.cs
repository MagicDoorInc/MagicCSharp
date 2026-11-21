using System.Reflection;
using MagicCSharp.Data.EntityFramework.Dals;
using Microsoft.EntityFrameworkCore;

namespace MagicCSharp.Data.EntityFramework.Repositories;

/// <summary>
///     Finds which property makes a DAL's rows a tenant's, and narrows a query to one tenant's rows by it. The one
///     place that reads <see cref="TenantKeyAttribute" /> at run time.
/// </summary>
public static class TenantRows
{
    /// <summary>
    ///     The tenant key property of <paramref name="dalType" />: the one its assembly names with
    ///     <c>[assembly: TenantKey("…")]</c>, when the DAL has it as a public <c>long</c> or <c>long?</c>. Null when the
    ///     assembly names none or the DAL has no such property — its rows belong to no tenant.
    /// </summary>
    public static string? FindKeyPropertyName(Type dalType)
    {
        var tenantKey = dalType.Assembly.GetCustomAttribute<TenantKeyAttribute>();
        if (tenantKey == null)
        {
            return null;
        }

        var property = dalType.GetProperty(tenantKey.PropertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null || (property.PropertyType != typeof(long) && property.PropertyType != typeof(long?)))
        {
            return null;
        }

        return property.Name;
    }

    /// <summary>The rows of <paramref name="query" /> whose tenant key property equals <paramref name="tenantId" />.</summary>
    /// <param name="query">The rows to narrow.</param>
    /// <param name="propertyName">The tenant key property, from <see cref="FindKeyPropertyName" />.</param>
    /// <param name="tenantId">The tenant whose rows are kept.</param>
    public static IQueryable<TDal> WhereTenant<TDal>(IQueryable<TDal> query, string propertyName, long tenantId)
    {
        var property = typeof(TDal).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance) ??
                       throw new ArgumentException($"{typeof(TDal).Name} has no public property {propertyName}.", nameof(propertyName));

        if (property.PropertyType == typeof(long?))
        {
            return query.Where(row => EF.Property<long?>(row!, propertyName) == tenantId);
        }

        return query.Where(row => EF.Property<long>(row!, propertyName) == tenantId);
    }
}
