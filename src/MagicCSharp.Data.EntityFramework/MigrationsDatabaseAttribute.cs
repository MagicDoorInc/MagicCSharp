using System.Reflection;

namespace MagicCSharp.Data.EntityFramework;

/// <summary>
///     The database this context's migrations target, written once on the context.
///     <para>
///         <c>dotnet ef</c> and the migration test both need it. With it, MagicCSharp.Generators writes the context's
///         design-time factory, and <c>MigrationUpgradeCheck</c> names its database and baseline after it.
///     </para>
///     <para>
///         By default the generated factory derives from <c>MagicCSharp.Data.Postgres.MagicDbContextFactory&lt;TContext&gt;</c>,
///         so it targets PostgreSQL and the project references that package. For another database, set the MSBuild
///         property <c>MagicCSharpDesignTimeContextFactoryBase</c> to a factory base of your own.
///     </para>
/// </summary>
/// <example>
///     <code language="csharp">
///     [MigrationsDatabase("shop")]
///     public class MagicShopContext(DbContextOptions&lt;MagicShopContext&gt; options) : MagicDbContext(options);
///     </code>
/// </example>
/// <param name="name">The database name.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MigrationsDatabaseAttribute(string name) : Attribute
{
    /// <summary>The database name.</summary>
    public string Name { get; } = name;

    /// <summary>The database <paramref name="contextType" /> names with this attribute.</summary>
    /// <exception cref="InvalidOperationException">The context has no <c>[MigrationsDatabase]</c>.</exception>
    public static string NameOf(Type contextType)
    {
        var attribute = contextType.GetCustomAttribute<MigrationsDatabaseAttribute>();
        if (attribute == null)
        {
            throw new InvalidOperationException($"{contextType.Name} has no [MigrationsDatabase(\"…\")]; add one naming its database.");
        }

        return attribute.Name;
    }
}
