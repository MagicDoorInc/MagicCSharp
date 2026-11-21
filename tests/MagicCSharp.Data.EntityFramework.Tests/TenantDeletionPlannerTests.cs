using MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;
using MagicCSharp.Data.EntityFramework.TenantDeletion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>The order tenant deletion runs destroyers in, read from the model; no database is opened.</summary>
public class TenantDeletionPlannerTests
{
    private readonly TenantDeletionPlanner planner = new TenantDeletionPlanner(ModelOnlyServices());

    [Fact]
    public void PlanDeletion_KeepsTheInputOrder_WhereNoForeignKeyDecides()
    {
        // Arrange: only the apartments' key to buildings orders anything.
        var buildings = StatedTable<BuildingDal>();
        var notices = StatedTable<NoticeDal>();
        var apartments = StatedTable<ApartmentDal>();

        // Act
        var tenantDeletionPlan = planner.PlanDeletion([buildings, notices, apartments]);

        // Assert: apartments move to just before their buildings; notices stay after buildings, as given.
        Assert.Equal([apartments, buildings, notices], tenantDeletionPlan.Steps);
    }

    [Fact]
    public void PlanDeletion_EmptiesAGrandchildBeforeItsChildAndItsChildBeforeTheParent_WhateverTheGivenOrder()
    {
        // Arrange: leases point at apartments, which point at buildings; given parent first.
        var buildings = StatedTable<BuildingDal>();
        var apartments = StatedTable<ApartmentDal>();
        var leases = StatedTable<LeaseDal>();

        // Act
        var tenantDeletionPlan = planner.PlanDeletion([buildings, apartments, leases]);

        // Assert
        Assert.Equal([leases, apartments, buildings], tenantDeletionPlan.Steps);
    }

    [Fact]
    public void PlanDeletion_MovesAParentAfterAChildGivenLater_AndKeepsTheChildsOwnChildBeforeIt()
    {
        // Arrange: the grandchild is given last, after an unrelated table.
        var apartments = StatedTable<ApartmentDal>();
        var notices = StatedTable<NoticeDal>();
        var leases = StatedTable<LeaseDal>();

        // Act
        var tenantDeletionPlan = planner.PlanDeletion([apartments, notices, leases]);

        // Assert: the apartments wait for their lease; notices stay where they were given.
        Assert.Equal([leases, apartments, notices], tenantDeletionPlan.Steps);
    }

    [Fact]
    public void PlanDeletion_ListsKeptTablesWithTheirReason_OutsideTheSteps()
    {
        // Arrange
        var auditEntries = StatedTable<AuditEntryDal>();
        var buildings = StatedTable<BuildingDal>();

        // Act
        var tenantDeletionPlan = planner.PlanDeletion([auditEntries, buildings]);

        // Assert
        Assert.Equal([buildings], tenantDeletionPlan.Steps);
        var keptTenantTable = Assert.Single(tenantDeletionPlan.Kept);
        Assert.Same(auditEntries, keptTenantTable.Destroyer);
        Assert.Equal(typeof(AuditEntryDal), keptTenantTable.DalType);
        Assert.Equal("The audit trail outlives the tenant it records", keptTenantTable.Reason);
    }

    [Fact]
    public void PlanDeletion_RunsTheFirstGivenDestroyerOfACycleLast()
    {
        // Arrange: each half holds a key to the other.
        var leftHalves = StatedTable<LeftHalfDal>();
        var rightHalves = StatedTable<RightHalfDal>();

        // Act
        var tenantDeletionPlan = planner.PlanDeletion([leftHalves, rightHalves]);

        // Assert
        Assert.Equal([rightHalves, leftHalves], tenantDeletionPlan.Steps);
    }

    [Fact]
    public void PlanDeletion_ThrowsNamingADestroyerThatDoesNotStateItsTable()
    {
        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            planner.PlanDeletion([StatedTable<BuildingDal>(), new UnstatedTableDestroyer()]));

        // Assert
        Assert.Contains("UnstatedTableDestroyer does not say which table it empties", exception.Message);
    }

    [Fact]
    public void PlanDeletion_ThrowsNamingADestroyerWhoseDalItsContextDoesNotMap()
    {
        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => planner.PlanDeletion([StatedTable<UnmappedDal>()]));

        // Assert
        Assert.Contains("StatedTableDestroyer names UnmappedDal, which TenantDeletionDbContext does not map", exception.Message);
    }

    private static StatedTableDestroyer StatedTable<TDal>()
    {
        return new StatedTableDestroyer(typeof(TDal));
    }

    private static IServiceProvider ModelOnlyServices()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<TenantDeletionDbContext>(options => options.UseNpgsql("Host=localhost;Database=never_opened"));
        return services.BuildServiceProvider();
    }
}
