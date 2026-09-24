using Microsoft.EntityFrameworkCore;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>
///     Derives from <see cref="MagicDbContext" /> rather than <c>DbContext</c> on purpose: the conventions
///     under test — enums stored by name, timestamps normalized to UTC — live there, so a context that
///     skipped it would test the repository against a schema no application uses.
/// </summary>
public class TestDbContext(DbContextOptions options) : MagicDbContext(options)
{
    public DbSet<WidgetDal> Widgets { get; set; } = null!;

    public DbSet<ContactDal> Contacts { get; set; } = null!;
}
