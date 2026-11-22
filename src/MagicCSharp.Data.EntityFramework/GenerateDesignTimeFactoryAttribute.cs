namespace MagicCSharp.Data.EntityFramework;

/// <summary>
///     Asks <c>MagicCSharp.Generators</c> to write this context's design-time factory, <c>{Context}Factory</c>, so
///     <c>dotnet ef</c> can build the context without the host. The factory connects to the database the context's
///     <see cref="MigrationsDatabaseAttribute" /> names, so the context carries both. The context need not be
///     <c>partial</c>: the factory is a class of its own.
///     <para>
///         By default the factory derives from <c>MagicCSharp.Data.Postgres.MagicDbContextFactory&lt;TContext&gt;</c>,
///         so it targets PostgreSQL and the project references that package. For another database, set the MSBuild
///         property <c>MagicCSharpDesignTimeContextFactoryBase</c> to a factory base of your own. A project that writes
///         its own factory for the context keeps it, e.g. to add a provider plugin.
///     </para>
/// </summary>
/// <example>
///     <code language="csharp">
///     [MigrationsDatabase("shop")]
///     [GenerateDesignTimeFactory]
///     public class MagicShopContext(DbContextOptions&lt;MagicShopContext&gt; options) : MagicDbContext(options);
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateDesignTimeFactoryAttribute : Attribute
{
}
