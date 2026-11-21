using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.EntityFramework.Tests.Fixtures.TenantDeletion;
using MagicCSharp.Data.EntityFramework.TenantDeletion;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Testing.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>Deleting a tenant on a real PostgreSQL, where a <c>Restrict</c> key refuses a wrong order.</summary>
[Trait("Category", "Database")]
public class TenantDataDestroyerTests : TestRepositoryBase<TenantDeletionDbContext>
{
    private const long DeletedTenantId = 7;
    private const long OtherTenantId = 8;

    private RepositoryServices<TenantDeletionDbContext> RepositoryServices
    {
        get
        {
            return new RepositoryServices<TenantDeletionDbContext>(DbContextFactory, KeyGen, TimeProvider, NullLoggerFactory.Instance);
        }
    }

    [Fact]
    public async Task DestroyTenant_EmptiesRestrictChildrenAndParents_WhenGivenParentFirst()
    {
        // Arrange
        await AddBuildingWithApartments(DeletedTenantId, 2);
        await AddBuildingWithApartments(OtherTenantId, 1);

        // Act
        var tenantDeletionResult = await TenantDataDestroyer().DestroyTenant(DeletedTenantId,
            [new BuildingsRepository(RepositoryServices), new ApartmentsRepository(RepositoryServices)]);

        // Assert
        Assert.Equal(2, tenantDeletionResult.SucceededCount);
        Assert.Equal(3, tenantDeletionResult.TotalDeleted);
        Assert.Equal(0, await CountRows<BuildingDal>(DeletedTenantId));
        Assert.Equal(0, await CountRows<ApartmentDal>(DeletedTenantId));
        Assert.Equal(1, await CountRows<BuildingDal>(OtherTenantId));
        Assert.Equal(1, await CountRows<ApartmentDal>(OtherTenantId));
    }

    [Fact]
    public async Task DestroyTenant_EmptiesCascadeChildrenToo_WhenTheirParentIsGivenFirst()
    {
        // Arrange
        await AddBuildingWithApartments(DeletedTenantId, 2);
        await AddPhotosOfEveryApartment(DeletedTenantId);
        await AddBuildingWithApartments(OtherTenantId, 1);
        await AddPhotosOfEveryApartment(OtherTenantId);

        // Act: apartments are given before their photos, and the database deletes the photos with them.
        var tenantDeletionResult = await TenantDataDestroyer().DestroyTenant(DeletedTenantId,
        [
            new ApartmentsRepository(RepositoryServices),
            new ApartmentPhotosRepository(RepositoryServices),
            new BuildingsRepository(RepositoryServices),
        ]);

        // Assert
        Assert.Equal(3, tenantDeletionResult.SucceededCount);
        Assert.Equal(3, tenantDeletionResult.TotalDeleted);
        Assert.Equal(0, await CountRows<ApartmentPhotoDal>(DeletedTenantId));
        Assert.Equal(0, await CountRows<ApartmentDal>(DeletedTenantId));
        Assert.Equal(0, await CountRows<BuildingDal>(DeletedTenantId));
        Assert.Equal(1, await CountRows<ApartmentPhotoDal>(OtherTenantId));
        Assert.Equal(1, await CountRows<ApartmentDal>(OtherTenantId));
        Assert.Equal(1, await CountRows<BuildingDal>(OtherTenantId));
    }

    [Fact]
    public async Task DestroyTenant_LeavesAKeptTablesRows_AndEmptiesTheWipedTable()
    {
        // Arrange
        await AddBuildingWithApartments(DeletedTenantId, 0);
        await new AuditEntriesRepository(RepositoryServices).Create(new AuditEntryEdit { TenantId = DeletedTenantId, Text = "Lease signed" });

        // Act
        var tenantDeletionResult = await TenantDataDestroyer().DestroyTenant(DeletedTenantId,
            [new AuditEntriesRepository(RepositoryServices), new BuildingsRepository(RepositoryServices)]);

        // Assert
        Assert.Equal(1, tenantDeletionResult.StepCount);
        Assert.Equal(1, await CountRows<AuditEntryDal>(DeletedTenantId));
        Assert.Equal(0, await CountRows<BuildingDal>(DeletedTenantId));
    }

    [Fact]
    public async Task DestroyTenant_RunsTheOthers_AndReportsNMinusOneOfN_WhenOneDestroyerFails()
    {
        // Arrange
        await AddBuildingWithApartments(DeletedTenantId, 1);

        // Act
        var tenantDeletionResult = await TenantDataDestroyer().DestroyTenant(DeletedTenantId,
        [
            new FailingTableDestroyer(typeof(NoticeDal)),
            new BuildingsRepository(RepositoryServices),
            new ApartmentsRepository(RepositoryServices),
        ]);

        // Assert
        Assert.Equal(3, tenantDeletionResult.StepCount);
        Assert.Equal(2, tenantDeletionResult.SucceededCount);
        Assert.Equal(0, await CountRows<BuildingDal>(DeletedTenantId));
        Assert.Equal(0, await CountRows<ApartmentDal>(DeletedTenantId));
    }

    [Fact]
    public async Task DestroyTenant_DeletesNothingAndSucceeds_WhenTheTenantIsDeletedAgain()
    {
        // Arrange
        await AddBuildingWithApartments(DeletedTenantId, 1);
        ITenantScopedDestroyer[] destroyers = [new BuildingsRepository(RepositoryServices), new ApartmentsRepository(RepositoryServices)];
        await TenantDataDestroyer().DestroyTenant(DeletedTenantId, destroyers);

        // Act
        var tenantDeletionResult = await TenantDataDestroyer().DestroyTenant(DeletedTenantId, destroyers);

        // Assert
        Assert.Equal(0, tenantDeletionResult.TotalDeleted);
        Assert.Equal(2, tenantDeletionResult.SucceededCount);
        Assert.Equal(2, tenantDeletionResult.StepCount);
    }

    [Fact]
    public async Task DestroyTenant_EmptiesTheTable_WhenTwoRepositoriesShareIt()
    {
        // Arrange
        await AddBuildingWithApartments(DeletedTenantId, 2);

        // Act
        var tenantDeletionResult = await TenantDataDestroyer().DestroyTenant(DeletedTenantId,
        [
            new ApartmentsRepository(RepositoryServices),
            new ApartmentArchiveRepository(RepositoryServices),
            new BuildingsRepository(RepositoryServices),
        ]);

        // Assert: the second repository of the table finds it empty and still succeeds.
        Assert.Equal(3, tenantDeletionResult.SucceededCount);
        Assert.Equal(3, tenantDeletionResult.TotalDeleted);
        Assert.Equal(0, await CountRows<ApartmentDal>(DeletedTenantId));
        Assert.Equal(0, await CountRows<BuildingDal>(DeletedTenantId));
    }

    private TenantDataDestroyer TenantDataDestroyer()
    {
        var services = new ServiceCollection();
        services.AddSingleton(DbContextFactory);
        services.AddLogging();
        services.AddTenantDeletion();
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<TenantDataDestroyer>();
    }

    private async Task AddBuildingWithApartments(long tenantId, int apartmentCount)
    {
        var building = await new BuildingsRepository(RepositoryServices).Create(new BuildingEdit { TenantId = tenantId, Name = "Maple Court" });
        for (var apartmentNumber = 1; apartmentNumber <= apartmentCount; apartmentNumber++)
        {
            await new ApartmentsRepository(RepositoryServices).Create(new ApartmentEdit
            {
                TenantId = tenantId,
                BuildingId = building.Id,
                Number = apartmentNumber.ToString(),
            });
        }
    }

    private async Task AddPhotosOfEveryApartment(long tenantId)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var apartmentIds = await context.Apartments.Where(apartment => apartment.TenantId == tenantId).Select(apartment => apartment.Id).ToListAsync();
        foreach (var apartmentId in apartmentIds)
        {
            await new ApartmentPhotosRepository(RepositoryServices).Create(new ApartmentPhotoEdit
            {
                TenantId = tenantId,
                ApartmentId = apartmentId,
                FileName = "kitchen.jpg",
            });
        }
    }

    private async Task<int> CountRows<TDal>(long tenantId)
        where TDal : class
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        return await context.Set<TDal>().CountAsync(row => EF.Property<long>(row, "TenantId") == tenantId);
    }
}
