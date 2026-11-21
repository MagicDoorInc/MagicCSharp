using System.Reflection;

namespace MagicCSharp.Testing;

/// <summary>
///     The entity's columns an in-memory repository writes itself, found by name once per entity type: <c>Id</c>,
///     <c>Created</c>, <c>Updated</c> and <c>Deleted</c>. By name rather than through an id interface, so an entity
///     whose id interface is the consumer's own works unchanged.
/// </summary>
internal static class EntityProperties<TEntity>
    where TEntity : class
{
    public const string IdName = "Id";

    public const string CreatedName = "Created";

    public const string UpdatedName = "Updated";

    public const string DeletedName = "Deleted";

    // object.MemberwiseClone is protected, so nameof cannot reach it from here.
    private const string MemberwiseCloneName = "MemberwiseClone";

    private static readonly Func<object, object> MemberwiseCloneOf = CreateMemberwiseClone();

    public static readonly PropertyInfo? IdProperty = FindWritableProperty(IdName, typeof(long));

    public static readonly PropertyInfo? CreatedProperty = FindWritableProperty(CreatedName, typeof(DateTimeOffset));

    public static readonly PropertyInfo? UpdatedProperty = FindWritableProperty(UpdatedName, typeof(DateTimeOffset));

    public static readonly PropertyInfo? DeletedProperty = FindWritableProperty(DeletedName, typeof(DateTimeOffset?));

    private static readonly Func<TEntity, long>? IdReader = CreateIdReader();

    public static long IdOf(TEntity entity)
    {
        if (IdReader == null)
        {
            throw new InvalidOperationException($"{typeof(TEntity).Name} has no public long {IdName} property with a setter or init.");
        }

        return IdReader(entity);
    }

    /// <summary>A shallow copy, as a record's <c>with</c> makes, so the stored row is never changed in place.</summary>
    public static TEntity Clone(TEntity entity)
    {
        return (TEntity)MemberwiseCloneOf(entity);
    }

    /// <summary>A public instance property of exactly this name and type, writable through a public setter or <c>init</c>.</summary>
    public static PropertyInfo? FindWritableProperty(string propertyName, Type propertyType)
    {
        return typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(property => property.Name == propertyName &&
                                        property.PropertyType == propertyType &&
                                        property.GetIndexParameters().Length == 0 &&
                                        property.SetMethod?.IsPublic == true);
    }

    private static Func<TEntity, long>? CreateIdReader()
    {
        if (IdProperty?.GetMethod == null)
        {
            return null;
        }

        return IdProperty.GetMethod.CreateDelegate<Func<TEntity, long>>();
    }

    private static Func<object, object> CreateMemberwiseClone()
    {
        var memberwiseClone = typeof(object).GetMethod(MemberwiseCloneName, BindingFlags.Instance | BindingFlags.NonPublic)!;

        return memberwiseClone.CreateDelegate<Func<object, object>>();
    }
}
