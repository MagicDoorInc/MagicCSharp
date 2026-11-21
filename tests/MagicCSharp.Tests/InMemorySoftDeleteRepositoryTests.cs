using MagicCSharp.Infrastructure.Entities;
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
    public async Task SoftDeleteSeveral_OverriddenSoftDelete_AppliesToEachKey()
    {
        var shippedParcelsKeptRepository = new ShippedParcelsKeptRepository(timeProvider);
        var receivedParcel = shippedParcelsKeptRepository.SeedStoredRow(ParcelEdit.Received("Received"));
        var shippedParcel = shippedParcelsKeptRepository.SeedStoredRow(ParcelEdit.Received("Shipped") with { Status = ParcelStatus.Shipped });
        timeProvider.Advance(TimeSpan.FromHours(1));

        await shippedParcelsKeptRepository.SoftDelete([receivedParcel.Id, shippedParcel.Id]);

        var expectedParcels = new[] { receivedParcel with { Deleted = Start.AddHours(1), Updated = Start.AddHours(1) }, shippedParcel };
        Assert.Equal(expectedParcels, shippedParcelsKeptRepository.StoredRowsByIdAscending);
    }

    [Fact]
    public async Task SoftDeleteByFilter_OverriddenSoftDelete_AppliesToEachMatch()
    {
        var shippedParcelsKeptRepository = new ShippedParcelsKeptRepository(timeProvider);
        var receivedParcel = shippedParcelsKeptRepository.SeedStoredRow(ParcelEdit.Received("Received"));
        var shippedParcel = shippedParcelsKeptRepository.SeedStoredRow(ParcelEdit.Received("Shipped") with { Status = ParcelStatus.Shipped });
        timeProvider.Advance(TimeSpan.FromHours(1));

        await shippedParcelsKeptRepository.SoftDelete(new ParcelFilter());

        var expectedParcels = new[] { receivedParcel with { Deleted = Start.AddHours(1), Updated = Start.AddHours(1) }, shippedParcel };
        Assert.Equal(expectedParcels, shippedParcelsKeptRepository.StoredRowsByIdAscending);
    }

    [Fact]
    public void Constructor_EntityWithoutWritableDeleted_ThrowsNamingEntityAndColumn()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new InMemorySoftDeleteRepository<StickerWithoutWritableDeleted, StickerWithoutWritableDeletedEdit, ParcelFilter>(timeProvider));

        Assert.Contains(nameof(StickerWithoutWritableDeleted), exception.Message);
        Assert.Contains("Deleted", exception.Message);
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A sticker whose <c>Deleted</c> has a getter only, so a soft delete has nowhere to write.</summary>
    public record StickerWithoutWritableDeleted : StickerWithoutWritableDeletedEdit, IMagicEntity, IDeletedEntity
    {
        public required long Id { get; init; }
        public required DateTimeOffset Created { get; init; }
        public required DateTimeOffset Updated { get; init; }
        public DateTimeOffset? Deleted { get; }
    }

    public record StickerWithoutWritableDeletedEdit
    {
        public required string Text { get; init; }
    }
}
