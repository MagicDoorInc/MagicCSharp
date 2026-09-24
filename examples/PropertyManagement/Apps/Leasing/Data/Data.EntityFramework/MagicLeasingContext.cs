using Acme.Leasing.Data.EntityFramework.Dals;
using MagicCSharp.Data.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Acme.Leasing.Data.EntityFramework;

/// <summary>
///     Deriving from MagicDbContext gets two conventions: enums stored by name, so inserting an enum member
///     does not change what existing rows mean, and timestamps normalized to UTC before they are written.
/// </summary>
public class MagicLeasingContext(DbContextOptions options) : MagicDbContext(options)
{
    // AddEntity adds a DbSet here for each entity it scaffolds.

    public DbSet<PropertyDal> Properties { get; set; } = null!;

    public DbSet<LeaseDal> Leases { get; set; } = null!;

    public DbSet<NotificationDal> Notifications { get; set; } = null!;

    public DbSet<ChargeDal> Charges { get; set; } = null!;

    public DbSet<LateFeePolicyDal> LateFeePolicies { get; set; } = null!;
}
