using MagicCSharp.Infrastructure.Exceptions;
using MagicCSharp.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace MagicCSharp.Tests;

/// <summary><see cref="InMemorySoftDeleteRepository{TEntity,TEdit,TFilter}" /> soft-deletes as the database's soft-delete repository does.</summary>
public class InMemorySoftDeleteRepositoryTests
{
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider timeProvider = new FakeTimeProvider(Start);

    [Fact]
    public async Task SoftDelete_StampsDeletedAndUpdated()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = await parcelsRepository.Create(ParcelEdit.Received("Blue box"));
        timeProvider.Advance(TimeSpan.FromHours(1));

        var deletedParcel = await parcelsRepository.SoftDelete(parcel.Id);

        Assert.Equal(parcel with { Deleted = Start.AddHours(1), Updated = Start.AddHours(1) }, deletedParcel);
        Assert.Equal(deletedParcel, await parcelsRepository.Get(parcel.Id));
    }

    [Fact]
    public async Task SoftDelete_AlreadyDeleted_StampsAgain()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = await parcelsRepository.Create(ParcelEdit.Received("Blue box"));
        await parcelsRepository.SoftDelete(parcel.Id);
        timeProvider.Advance(TimeSpan.FromHours(2));

        var deletedParcel = await parcelsRepository.SoftDelete(parcel.Id);

        Assert.Equal(Start.AddHours(2), deletedParcel.Deleted);
        Assert.Equal(Start.AddHours(2), deletedParcel.Updated);
    }

    [Fact]
    public async Task SoftDelete_MissingId_ThrowsNotFound()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);

        var exception = await Assert.ThrowsAsync<NotFoundIdException>(() => parcelsRepository.SoftDelete(99));

        Assert.Equal(99, exception.Id);
    }

    [Fact]
    public async Task SoftDeleteSeveral_OneMissing_DeletesNothing()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = parcelsRepository.SeedStoredRow(ParcelEdit.Received("Blue box"));

        await Assert.ThrowsAsync<NotFoundIdException>(() => parcelsRepository.SoftDelete([parcel.Id, 99]));

        Assert.Equal(new[] { parcel }, parcelsRepository.StoredRowsByIdAscending);
    }

    [Fact]
    public async Task SoftDeleteByFilter_MarksMatches()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var receivedParcel = parcelsRepository.SeedStoredRow(ParcelEdit.Received("Received"));
        var shippedParcel = parcelsRepository.SeedStoredRow(ParcelEdit.Received("Shipped") with { Status = ParcelStatus.Shipped });
        timeProvider.Advance(TimeSpan.FromHours(1));

        var deletedRowCount = await parcelsRepository.SoftDelete(new ParcelFilter { Statuses = [ParcelStatus.Shipped] });

        Assert.Equal(1, deletedRowCount);
        var expectedParcels = new[] { receivedParcel, shippedParcel with { Deleted = Start.AddHours(1), Updated = Start.AddHours(1) } };
        Assert.Equal(expectedParcels, parcelsRepository.StoredRowsByIdAscending);
    }

    [Fact]
    public async Task DestroyTenant_RemovesSoftDeletedRows()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = parcelsRepository.SeedStoredRow(ParcelEdit.Received("Blue box"));
        await parcelsRepository.SoftDelete(parcel.Id);
        var otherWarehouseParcel = parcelsRepository.SeedStoredRow(ParcelEdit.Received("Other warehouse") with { WarehouseId = ParcelEdit.OtherWarehouse });

        var destroyedRowCount = await parcelsRepository.DestroyTenant(ParcelEdit.MainWarehouse);

        Assert.Equal(1, destroyedRowCount);
        Assert.Equal(new[] { otherWarehouseParcel }, parcelsRepository.StoredRowsByIdAscending);
    }
}
