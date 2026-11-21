using MagicCSharp.Data.Models;
using MagicCSharp.Infrastructure.Entities;
using MagicCSharp.Infrastructure.Exceptions;
using MagicCSharp.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace MagicCSharp.Tests;

/// <summary>
///     <see cref="InMemoryRepository{TEntity,TEdit,TFilter}" /> answers as the database repositories do, and a fake's
///     overrides take effect on every path that reaches them.
/// </summary>
public class InMemoryRepositoryTests
{
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider timeProvider = new FakeTimeProvider(Start);

    [Fact]
    public async Task Create_CopiesEditFieldsAndStampsIdAndTimes()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);

        var parcel = await parcelsRepository.Create(ParcelEdit.Received("Blue box") with { Shelved = Start.AddHours(-1) });

        var expectedParcel = new Parcel
        {
            Id = 1,
            Created = Start,
            Updated = Start,
            WarehouseId = ParcelEdit.MainWarehouse,
            Status = ParcelStatus.Received,
            Label = "Blue box",
            Shelved = Start.AddHours(-1),
        };
        Assert.Equal(expectedParcel, parcel);
        Assert.Equal(expectedParcel, await parcelsRepository.Get(1));
    }

    [Fact]
    public async Task CreateSeveral_ReturnsIdsInEditOrder()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);

        var ids = await parcelsRepository.Create([ParcelEdit.Received("First"), ParcelEdit.Received("Second")]);

        Assert.Equal(new long[] { 1, 2 }, ids);
        Assert.Equal(new[] { "First", "Second" }, parcelsRepository.StoredRowsByIdAscending.Select(parcel => parcel.Label));
    }

    [Fact]
    public async Task FirstId_StartsIdsThere()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider, 500);

        var parcel = await parcelsRepository.Create(ParcelEdit.Received("Blue box"));

        Assert.Equal(500, parcel.Id);
    }

    [Fact]
    public async Task GetByFilter_ReturnsMatchesNewestFirst()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("First"));
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("Other warehouse") with { WarehouseId = ParcelEdit.OtherWarehouse });
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("Third"));

        var parcels = await parcelsRepository.Get(new ParcelFilter { WarehouseId = ParcelEdit.MainWarehouse });

        Assert.Equal(new[] { "Third", "First" }, parcels.Select(parcel => parcel.Label));
    }

    [Fact]
    public async Task GetByFilter_OverriddenOrder_UsesIt()
    {
        var oldestFirstParcelsRepository = new OldestFirstParcelsRepository(timeProvider);
        oldestFirstParcelsRepository.SeedStoredRow(ParcelEdit.Received("First"));
        oldestFirstParcelsRepository.SeedStoredRow(ParcelEdit.Received("Second"));

        var parcels = await oldestFirstParcelsRepository.Get(new ParcelFilter());

        Assert.Equal(new[] { "First", "Second" }, parcels.Select(parcel => parcel.Label));
    }

    [Fact]
    public async Task GetByFilter_FilterWithoutMatches_ThrowsNamingFilter()
    {
        var inMemoryRepository = new InMemoryRepository<Parcel, ParcelEdit, ParcelLabelSearch>(timeProvider);
        inMemoryRepository.SeedStoredRow(ParcelEdit.Received("Blue box"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => inMemoryRepository.Get(new ParcelLabelSearch { LabelStart = "Blue" }));

        Assert.Contains(nameof(ParcelLabelSearch), exception.Message);
    }

    [Fact]
    public async Task GetByFilter_OverriddenMatches_UsesIt()
    {
        var labelSearchParcelsRepository = new LabelSearchParcelsRepository(timeProvider);
        labelSearchParcelsRepository.SeedStoredRow(ParcelEdit.Received("Blue box"));
        labelSearchParcelsRepository.SeedStoredRow(ParcelEdit.Received("Red box"));

        var parcels = await labelSearchParcelsRepository.Get(new ParcelLabelSearch { LabelStart = "Blue" });

        Assert.Equal(new[] { "Blue box" }, parcels.Select(parcel => parcel.Label));
    }

    [Fact]
    public async Task GetByIds_SkipsMissing()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = parcelsRepository.SeedStoredRow(ParcelEdit.Received("Blue box"));

        var parcels = await parcelsRepository.Get([parcel.Id, 99]);

        Assert.Equal(new[] { parcel }, parcels);
    }

    [Fact]
    public async Task Count_CountsMatches()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("Received"));
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("Shipped") with { Status = ParcelStatus.Shipped });
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("Also shipped") with { Status = ParcelStatus.Shipped });

        var shippedCount = await parcelsRepository.Count(new ParcelFilter { Statuses = [ParcelStatus.Shipped] });

        Assert.Equal(2, shippedCount);
    }

    [Fact]
    public async Task GetKeys_ReturnsMatchingIds()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("Received"));
        var shippedParcel = parcelsRepository.SeedStoredRow(ParcelEdit.Received("Shipped") with { Status = ParcelStatus.Shipped });

        var keys = await parcelsRepository.GetKeys(new ParcelFilter { Statuses = [ParcelStatus.Shipped] });

        Assert.Equal(new[] { shippedParcel.Id }, keys);
    }

    [Fact]
    public async Task GetPage_ReturnsPageAndTotal()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("First"));
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("Second"));
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("Third"));

        var page = await parcelsRepository.Get(new PaginationRequest(2, 2), new ParcelFilter());

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(new[] { "First" }, page.Items.Select(parcel => parcel.Label));
    }

    [Fact]
    public async Task GetPage_Disabled_ReturnsAll()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("First"));
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("Second"));

        var page = await parcelsRepository.Get(new PaginationRequest(true) { PageSize = 1 }, new ParcelFilter());

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { "Second", "First" }, page.Items.Select(parcel => parcel.Label));
    }

    [Fact]
    public async Task Update_KeepsFieldsTheEditLacks()
    {
        var scannedParcelsRepository = new ScannedParcelsRepository(timeProvider);
        var parcel = await scannedParcelsRepository.Create(ParcelEdit.Received("Blue box"));

        var updatedParcel = await scannedParcelsRepository.Update(parcel.Id, ParcelEdit.Received("Blue box") with { Status = ParcelStatus.Shelved });

        Assert.Equal(ScannedParcelsRepository.ScanNoteOnArrival, updatedParcel.ScanNote);
        Assert.Equal(ParcelStatus.Shelved, updatedParcel.Status);
    }

    [Fact]
    public async Task Update_Changed_StampsUpdated()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = await parcelsRepository.Create(ParcelEdit.Received("Blue box"));
        timeProvider.Advance(TimeSpan.FromHours(1));

        var updatedParcel = await parcelsRepository.Update(parcel.Id, ParcelEdit.Received("Blue box") with { Status = ParcelStatus.Shelved });

        Assert.Equal(Start, updatedParcel.Created);
        Assert.Equal(Start.AddHours(1), updatedParcel.Updated);
        Assert.Equal(updatedParcel, await parcelsRepository.Get(parcel.Id));
    }

    [Fact]
    public async Task Update_Unchanged_KeepsUpdated()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = await parcelsRepository.Create(ParcelEdit.Received("Blue box"));
        timeProvider.Advance(TimeSpan.FromHours(1));

        var updatedParcel = await parcelsRepository.Update(parcel.Id, ParcelEdit.Received("Blue box"));

        Assert.Equal(Start, updatedParcel.Updated);
    }

    [Fact]
    public async Task Update_KeepsASetOnceProperty()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = await parcelsRepository.Create(ParcelEdit.Received("Blue box"));

        var updatedParcel = await parcelsRepository.Update(parcel.Id, ParcelEdit.Received("Blue box") with { WarehouseId = ParcelEdit.OtherWarehouse, Status = ParcelStatus.Shelved });

        Assert.Equal(ParcelEdit.MainWarehouse, updatedParcel.WarehouseId);
    }

    [Fact]
    public async Task Update_MissingId_ThrowsNotFound()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);

        var exception = await Assert.ThrowsAsync<NotFoundIdException>(() => parcelsRepository.Update(99, ParcelEdit.Received("Blue box")));

        Assert.Equal(99, exception.Id);
    }

    [Fact]
    public async Task UpdateSeveral_OneMissing_WritesNothing()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = await parcelsRepository.Create(ParcelEdit.Received("Blue box"));
        var edits = new Dictionary<long, ParcelEdit>
        {
            [parcel.Id] = ParcelEdit.Received("Blue box") with { Status = ParcelStatus.Shelved },
            [99] = ParcelEdit.Received("Missing"),
        };

        await Assert.ThrowsAsync<NotFoundIdException>(() => parcelsRepository.Update(edits));

        Assert.Equal(parcel, await parcelsRepository.Get(parcel.Id));
    }

    [Fact]
    public async Task UpdateSeveral_CountsChangedRows()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var changingParcel = await parcelsRepository.Create(ParcelEdit.Received("Changing"));
        var unchangedParcel = await parcelsRepository.Create(ParcelEdit.Received("Unchanged"));
        var edits = new Dictionary<long, ParcelEdit>
        {
            [changingParcel.Id] = ParcelEdit.Received("Changing") with { Status = ParcelStatus.Shelved },
            [unchangedParcel.Id] = ParcelEdit.Received("Unchanged"),
        };

        var changedRowCount = await parcelsRepository.Update(edits);

        Assert.Equal(1, changedRowCount);
    }

    [Fact]
    public async Task UpdateEntities_CountsChangedRows()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var changingParcel = await parcelsRepository.Create(ParcelEdit.Received("Changing"));
        var unchangedParcel = await parcelsRepository.Create(ParcelEdit.Received("Unchanged"));
        timeProvider.Advance(TimeSpan.FromHours(1));

        var changedRowCount = await parcelsRepository.Update([changingParcel with { Status = ParcelStatus.Shelved }, unchangedParcel]);

        Assert.Equal(1, changedRowCount);
        Assert.Equal(new[] { changingParcel with { Status = ParcelStatus.Shelved, Updated = Start.AddHours(1) }, unchangedParcel }, parcelsRepository.StoredRowsByIdAscending);
    }

    [Fact]
    public async Task UpdateEntities_OneMissing_WritesNothing()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = await parcelsRepository.Create(ParcelEdit.Received("Blue box"));

        var exception = await Assert.ThrowsAsync<NotFoundIdException>(() => parcelsRepository.Update([parcel with { Status = ParcelStatus.Shelved }, parcel with { Id = 99 }]));

        Assert.Equal(99, exception.Id);
        Assert.Equal(new[] { parcel }, parcelsRepository.StoredRowsByIdAscending);
    }

    [Fact]
    public async Task CreateSeveral_OverriddenCreate_AppliesToEachEdit()
    {
        var relabellingParcelsRepository = new RelabellingParcelsRepository(timeProvider);

        await relabellingParcelsRepository.Create([ParcelEdit.Received("First"), ParcelEdit.Received("Second")]);

        Assert.Equal(new[] { "First (relabelled)", "Second (relabelled)" }, relabellingParcelsRepository.StoredRowsByIdAscending.Select(parcel => parcel.Label));
    }

    [Fact]
    public async Task UpdateSeveral_OverriddenUpdate_AppliesToEachEdit()
    {
        var relabellingParcelsRepository = new RelabellingParcelsRepository(timeProvider);
        var parcel = relabellingParcelsRepository.SeedStoredRow(ParcelEdit.Received("Blue box"));

        var changedRowCount = await relabellingParcelsRepository.Update(new Dictionary<long, ParcelEdit> { [parcel.Id] = ParcelEdit.Received("Blue box") });

        Assert.Equal(1, changedRowCount);
        Assert.Equal("Blue box (relabelled)", (await relabellingParcelsRepository.Get(parcel.Id))?.Label);
    }

    [Fact]
    public async Task UpdateEntities_OverriddenUpdate_AppliesToEachEntity()
    {
        var relabellingParcelsRepository = new RelabellingParcelsRepository(timeProvider);
        var parcel = relabellingParcelsRepository.SeedStoredRow(ParcelEdit.Received("Blue box"));

        var changedRowCount = await relabellingParcelsRepository.Update([parcel]);

        Assert.Equal(1, changedRowCount);
        Assert.Equal("Blue box (relabelled)", (await relabellingParcelsRepository.Get(parcel.Id))?.Label);
    }

    [Fact]
    public async Task DeleteSeveral_OverriddenDelete_AppliesToEachKey()
    {
        var shippedParcelsKeptRepository = new ShippedParcelsKeptRepository(timeProvider);
        var receivedParcel = shippedParcelsKeptRepository.SeedStoredRow(ParcelEdit.Received("Received"));
        var shippedParcel = shippedParcelsKeptRepository.SeedStoredRow(ParcelEdit.Received("Shipped") with { Status = ParcelStatus.Shipped });

        var deletedRowCount = await shippedParcelsKeptRepository.Delete([receivedParcel.Id, shippedParcel.Id]);

        Assert.Equal(1, deletedRowCount);
        Assert.Equal(new[] { shippedParcel }, shippedParcelsKeptRepository.StoredRowsByIdAscending);
    }

    [Fact]
    public async Task DeleteByFilter_OverriddenDelete_AppliesToEachMatch()
    {
        var shippedParcelsKeptRepository = new ShippedParcelsKeptRepository(timeProvider);
        shippedParcelsKeptRepository.SeedStoredRow(ParcelEdit.Received("Received"));
        var shippedParcel = shippedParcelsKeptRepository.SeedStoredRow(ParcelEdit.Received("Shipped") with { Status = ParcelStatus.Shipped });

        var deletedRowCount = await shippedParcelsKeptRepository.Delete(new ParcelFilter());

        Assert.Equal(1, deletedRowCount);
        Assert.Equal(new[] { shippedParcel }, shippedParcelsKeptRepository.StoredRowsByIdAscending);
    }

    [Fact]
    public async Task UpdateEntity_WritesEditFields()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = await parcelsRepository.Create(ParcelEdit.Received("Blue box"));

        await parcelsRepository.Update(parcel with { Status = ParcelStatus.Shipped, Label = "Blue box, taped" });

        var storedParcel = await parcelsRepository.Get(parcel.Id);
        Assert.NotNull(storedParcel);
        Assert.Equal(ParcelStatus.Shipped, storedParcel.Status);
        Assert.Equal("Blue box, taped", storedParcel.Label);
    }

    [Fact]
    public async Task Update_OverriddenCopyWithEdit_StoresItsEntity()
    {
        var labelLockedParcelsRepository = new LabelLockedParcelsRepository(timeProvider);
        var parcel = await labelLockedParcelsRepository.Create(ParcelEdit.Received("Blue box"));

        await labelLockedParcelsRepository.Update(parcel.Id, ParcelEdit.Received("Red box") with { Status = ParcelStatus.Shelved });

        var storedParcel = await labelLockedParcelsRepository.Get(parcel.Id);
        Assert.NotNull(storedParcel);
        Assert.Equal("Blue box", storedParcel.Label);
        Assert.Equal(ParcelStatus.Shelved, storedParcel.Status);
    }

    [Fact]
    public async Task Create_OverriddenCreateEntity_StoresItsEntity()
    {
        var scannedParcelsRepository = new ScannedParcelsRepository(timeProvider);

        var parcel = await scannedParcelsRepository.Create(ParcelEdit.Received("Blue box"));

        var storedParcel = await scannedParcelsRepository.Get(parcel.Id);
        Assert.NotNull(storedParcel);
        Assert.Equal(ScannedParcelsRepository.ScanNoteOnArrival, storedParcel.ScanNote);
    }

    [Fact]
    public async Task Delete_MissingId_ThrowsNotFound()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);

        var exception = await Assert.ThrowsAsync<NotFoundIdException>(() => parcelsRepository.Delete(99));

        Assert.Equal(99, exception.Id);
    }

    [Fact]
    public async Task DeleteSeveral_OneMissing_DeletesNothing()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var parcel = parcelsRepository.SeedStoredRow(ParcelEdit.Received("Blue box"));

        await Assert.ThrowsAsync<NotFoundIdException>(() => parcelsRepository.Delete([parcel.Id, 99]));

        Assert.Equal(new[] { parcel }, parcelsRepository.StoredRowsByIdAscending);
    }

    [Fact]
    public async Task DeleteByFilter_RemovesMatches()
    {
        var parcelsRepository = new ParcelsRepository(timeProvider);
        var receivedParcel = parcelsRepository.SeedStoredRow(ParcelEdit.Received("Received"));
        parcelsRepository.SeedStoredRow(ParcelEdit.Received("Shipped") with { Status = ParcelStatus.Shipped });

        var deletedRowCount = await parcelsRepository.Delete(new ParcelFilter { Statuses = [ParcelStatus.Shipped] });

        Assert.Equal(1, deletedRowCount);
        Assert.Equal(new[] { receivedParcel }, parcelsRepository.StoredRowsByIdAscending);
    }

    [Fact]
    public async Task OverriddenNotFound_IsThrown()
    {
        var shelfParcelsRepository = new ShelfParcelsRepository(timeProvider);

        var exception = await Assert.ThrowsAsync<ParcelNotOnShelfException>(() => shelfParcelsRepository.Update(99, ParcelEdit.Received("Blue box")));

        Assert.Equal(99, exception.ParcelId);
    }

    [Fact]
    public async Task SeedStoredRow_IgnoresFailingCreateOverride()
    {
        var refusingParcelsRepository = new RefusingParcelsRepository(timeProvider);

        var parcel = refusingParcelsRepository.SeedStoredRow(ParcelEdit.Received("Blue box"));

        Assert.Equal(new[] { parcel }, refusingParcelsRepository.StoredRowsByIdAscending);
        await Assert.ThrowsAsync<InvalidOperationException>(() => refusingParcelsRepository.Create(ParcelEdit.Received("Red box")));
    }

    [Fact]
    public void Constructor_EntityWithoutLongId_ThrowsNamingEntity()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new InMemoryRepository<Sticker, StickerEdit, ParcelFilter>(timeProvider));

        Assert.Contains(nameof(Sticker), exception.Message);
    }

    [Fact]
    public void Constructor_EntityWithoutWritableCreated_ThrowsNamingEntityAndColumn()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new InMemoryRepository<StickerWithoutWritableCreated, StickerEdit, ParcelFilter>(timeProvider));

        Assert.Contains(nameof(StickerWithoutWritableCreated), exception.Message);
        Assert.Contains("Created", exception.Message);
    }

    [Fact]
    public void Constructor_EntityWithoutWritableUpdated_ThrowsNamingEntityAndColumn()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new InMemoryRepository<StickerWithoutWritableUpdated, StickerEdit, ParcelFilter>(timeProvider));

        Assert.Contains(nameof(StickerWithoutWritableUpdated), exception.Message);
        Assert.Contains("Updated", exception.Message);
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A filter that doesn't list <c>IEntityFilter</c>, so it has no generated <c>Matches</c>.</summary>
    public class ParcelLabelSearch
    {
        public required string LabelStart { get; init; }
    }

    public class OldestFirstParcelsRepository(TimeProvider timeProvider) : ParcelsRepository(timeProvider)
    {
        protected override IEnumerable<Parcel> ApplyOrder(IEnumerable<Parcel> entities)
        {
            return entities.OrderBy(parcel => parcel.Id);
        }
    }

    public class LabelSearchParcelsRepository(TimeProvider timeProvider) : InMemoryRepository<Parcel, ParcelEdit, ParcelLabelSearch>(timeProvider)
    {
        protected override bool Matches(Parcel entity, ParcelLabelSearch filter)
        {
            return entity.Label.StartsWith(filter.LabelStart, StringComparison.Ordinal);
        }
    }

    /// <summary>Notes every parcel as scanned when it arrives, a field the edit doesn't hold.</summary>
    public class ScannedParcelsRepository(TimeProvider timeProvider) : ParcelsRepository(timeProvider)
    {
        public const string ScanNoteOnArrival = "Scanned at the loading dock";

        protected override Parcel CreateEntity(ParcelEdit edit, long id, DateTimeOffset created)
        {
            return base.CreateEntity(edit, id, created) with { ScanNote = ScanNoteOnArrival };
        }
    }

    /// <summary>A parcel's label is printed once and never changes.</summary>
    public class LabelLockedParcelsRepository(TimeProvider timeProvider) : ParcelsRepository(timeProvider)
    {
        protected override Parcel CopyWithEdit(Parcel entity, ParcelEdit edit)
        {
            return base.CopyWithEdit(entity, edit) with { Label = entity.Label };
        }
    }

    /// <summary>Appends " (relabelled)" to the label of every parcel it creates or updates.</summary>
    public class RelabellingParcelsRepository(TimeProvider timeProvider) : ParcelsRepository(timeProvider)
    {
        public override Task<Parcel> Create(ParcelEdit edit)
        {
            return base.Create(edit with { Label = edit.Label + " (relabelled)" });
        }

        public override Task<Parcel> Update(long key, ParcelEdit edit)
        {
            return base.Update(key, edit with { Label = edit.Label + " (relabelled)" });
        }
    }

    public class ShelfParcelsRepository(TimeProvider timeProvider) : ParcelsRepository(timeProvider)
    {
        protected override Exception CreateNotFoundException(long id)
        {
            return new ParcelNotOnShelfException(id);
        }
    }

    public class ParcelNotOnShelfException(long parcelId) : Exception("No parcel " + parcelId + " on the shelf.")
    {
        public long ParcelId { get; } = parcelId;
    }

    /// <summary>A storage that refuses every new parcel.</summary>
    public class RefusingParcelsRepository(TimeProvider timeProvider) : ParcelsRepository(timeProvider)
    {
        public override Task<Parcel> Create(ParcelEdit edit)
        {
            throw new InvalidOperationException("The storage refused the parcel.");
        }
    }

    /// <summary>An entity keyed by a string, which an in-memory repository of long ids can't keep.</summary>
    public record Sticker : StickerEdit, IMagicEntity
    {
        public required string Id { get; init; }
        public required DateTimeOffset Created { get; init; }
        public required DateTimeOffset Updated { get; init; }
    }

    /// <summary>A sticker whose <c>Created</c> is only reachable through the interface, so no public setter or init carries it.</summary>
    public record StickerWithoutWritableCreated : StickerEdit, IMagicEntity
    {
        public required long Id { get; init; }
        DateTimeOffset IMagicEntity.Created { get; init; }
        public required DateTimeOffset Updated { get; init; }
    }

    /// <summary>A sticker whose <c>Updated</c> is only reachable through the interface, so no public setter or init carries it.</summary>
    public record StickerWithoutWritableUpdated : StickerEdit, IMagicEntity
    {
        public required long Id { get; init; }
        public required DateTimeOffset Created { get; init; }
        DateTimeOffset IMagicEntity.Updated { get; init; }
    }

    public record StickerEdit
    {
        public required string Text { get; init; }
    }
}
