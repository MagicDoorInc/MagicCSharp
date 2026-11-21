using Microsoft.EntityFrameworkCore;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;

/// <summary>The tables tenant deletion is tested on: a Restrict parent and child, a kept table, and ordering-only tables.</summary>
public class TenantDeletionDbContext(DbContextOptions options) : MagicDbContext(options)
{
    public DbSet<BuildingDal> Buildings { get; set; } = null!;

    public DbSet<ApartmentDal> Apartments { get; set; } = null!;

    public DbSet<AuditEntryDal> AuditEntries { get; set; } = null!;

    public DbSet<NoticeDal> Notices { get; set; } = null!;

    public DbSet<LeftHalfDal> LeftHalves { get; set; } = null!;

    public DbSet<RightHalfDal> RightHalves { get; set; } = null!;
}
