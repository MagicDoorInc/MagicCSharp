using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MagicCSharp.Data.EntityFramework;

/// <summary>
///     A <see cref="DbContext" /> with the two conventions the repository bases rely on.
///     <para>
///         <b>Enums are stored as their names.</b> By default EF stores an enum as its ordinal, so inserting a new
///         member in the middle of the declaration silently changes the meaning of every row already written.
///         Storing the name makes the column readable in a query tool and makes reordering harmless; only renaming
///         a member is then a migration.
///     </para>
///     <para>
///         <b>Timestamps are converted to UTC before they are written.</b> A <see cref="DateTimeOffset" /> carrying
///         a local offset stays a correct instant but comes back with a different offset than it went in with, and
///         some providers reject a non-zero offset outright. Normalizing on the way in means every row holds the
///         same shape regardless of where the process ran.
///     </para>
/// </summary>
public abstract class MagicDbContext(DbContextOptions options) : DbContext(options)
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> TimestampPropertiesByType = [];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var propertyType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (propertyType.IsEnum)
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasConversion<string>()
                        .HasColumnType("VARCHAR(100)");
                }

                // An enum inside a JSON column is mapped separately, and EF still writes those as integers
                // (dotnet/efcore#31100). Convert at the element so a JSON document and a plain column agree
                // on what an enum looks like.
                var elementType = property.GetElementType();
                if (elementType == null)
                {
                    continue;
                }

                var elementClrType = Nullable.GetUnderlyingType(elementType.ClrType) ?? elementType.ClrType;
                if (elementClrType.IsEnum)
                {
                    var converterType = typeof(EnumToStringConverter<>).MakeGenericType(elementClrType);
                    elementType.SetValueConverter((ValueConverter)Activator.CreateInstance(converterType)!);
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        NormalizeTimestampsToUtc();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        NormalizeTimestampsToUtc();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeTimestampsToUtc();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        NormalizeTimestampsToUtc();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void NormalizeTimestampsToUtc()
    {
        var entries = ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            // Reflecting over every property of every changed entity on every save is expensive, and the
            // shape of a type never changes, so the property list is worked out once per type.
            var properties = TimestampPropertiesByType.GetOrAdd(entry.Entity.GetType(), type => type.GetProperties()
                .Where(property => property.PropertyType == typeof(DateTimeOffset) || property.PropertyType == typeof(DateTimeOffset?))
                .Where(property => property is { CanRead: true, CanWrite: true })
                .ToArray());

            foreach (var property in properties)
            {
                if (property.GetValue(entry.Entity) is DateTimeOffset value && value.Offset != TimeSpan.Zero)
                {
                    property.SetValue(entry.Entity, value.ToUniversalTime());
                }
            }
        }
    }
}
