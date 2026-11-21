using Microsoft.EntityFrameworkCore;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.Boilers;

/// <summary>A context with two migrations, for the migration check; its design-time factory is generated.</summary>
[MigrationsDatabase("boilers")]
public class BoilersDbContext(DbContextOptions<BoilersDbContext> options) : MagicDbContext(options)
{
    public DbSet<BoilerDal> Boilers { get; set; } = null!;
}
