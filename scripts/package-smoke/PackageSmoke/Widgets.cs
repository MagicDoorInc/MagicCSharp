using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MagicCSharp.Data.EntityFramework;
using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace PackageSmoke;

public record Widget : WidgetEdit, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

public record WidgetEdit
{
    public required long OwnerId { get; init; }
    public required string Name { get; init; }
}

/// <summary><c>partial</c> and <c>IEntityFilter</c>: the generator writes <c>Matches</c>.</summary>
public partial class WidgetFilter : IEntityFilter<Widget>
{
    public List<long>? Ids { get; init; }
    public long? OwnerId { get; init; }
}

/// <summary><c>partial</c>, with columns only: the generator writes <c>ToEntity</c>, <c>Apply</c> and <c>From</c>.</summary>
[Table("widgets")]
public partial class WidgetDal : BaseIdDal<Widget, WidgetEdit>
{
    [Required]
    [Column("owner_id")]
    public long OwnerId { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = "";
}

/// <summary>The generator writes <c>SmokeContextFactory</c> for <c>dotnet ef</c> from the attribute.</summary>
[MigrationsDatabase("smoke")]
public class SmokeContext(DbContextOptions<SmokeContext> options) : MagicDbContext(options)
{
    public DbSet<WidgetDal> Widgets { get; set; } = null!;
}

/// <summary><c>partial</c> on the repository services: <c>ApplyFilter</c> is generated and <c>CreateDal</c> is the base's default.</summary>
public partial class WidgetsRepository(RepositoryServices<SmokeContext> repositoryServices)
    : BaseIdRepository<SmokeContext, WidgetDal, Widget, WidgetFilter, WidgetEdit>(repositoryServices)
{
    /// <summary>The SQL the generated <c>ApplyFilter</c> gives, without a database.</summary>
    public string ToFilterSql(SmokeContext context, WidgetFilter filter)
    {
        return ApplyFilter(context.Widgets, filter).ToQueryString();
    }

    /// <summary>A new row through the base's default <c>CreateDal</c>, which calls the generated <c>From</c>.</summary>
    public WidgetDal CreateRow(WidgetEdit edit)
    {
        return CreateDal(edit);
    }
}

/// <summary>A context factory built from the generated design-time factory; the contexts never open a connection here.</summary>
public class ContextFactoryFromDesignTimeFactory : IDbContextFactory<SmokeContext>
{
    public SmokeContext CreateDbContext()
    {
        return new SmokeContextFactory().CreateDbContext([]);
    }
}
