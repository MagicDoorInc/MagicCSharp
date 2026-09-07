using MagicCSharp.Data.EntityFramework.Tests.Fixtures;
using MagicCSharp.Infrastructure.Exceptions;

namespace MagicCSharp.Data.EntityFramework.Tests;

public class BaseIdRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task Create_writes_every_field()
    {
        var created = await Widgets.Create(AWidget("Sprocket", "A round one", 7, WidgetStatus.Active));

        var read = await Widgets.Get(created.Id);

        Assert.NotNull(read);
        Assert.Equal("Sprocket", read.Name);
        Assert.Equal("A round one", read.Description);
        Assert.Equal(7, read.Quantity);
        Assert.Equal(WidgetStatus.Active, read.Status);
    }

    [Fact]
    public async Task Create_assigns_an_id_before_the_insert()
    {
        // The id comes from the key generator, not a sequence, which is what lets a caller know it without
        // a round trip. A database-generated identity would break ids being unique across instances.
        var created = await Widgets.Create(AWidget());

        Assert.NotEqual(0, created.Id);
    }

    [Fact]
    public async Task Create_stamps_created_and_updated_from_the_clock()
    {
        Clock.SetTime(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

        var created = await Widgets.Create(AWidget());

        Assert.Equal(Clock.Now(), created.Created);
        Assert.Equal(Clock.Now(), created.Updated);
    }

    [Fact]
    public async Task Timestamps_come_back_as_utc()
    {
        // MagicDbContext normalizes to UTC on the way in. Round-tripping through Postgres is the only way
        // to know the interceptor is actually attached.
        Clock.SetTime(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.FromHours(5)));

        var created = await Widgets.Create(AWidget());
        var read = await Widgets.Get(created.Id);

        Assert.Equal(TimeSpan.Zero, read!.Created.Offset);
        Assert.Equal(Clock.Now().ToUniversalTime(), read.Created);
    }

    [Fact]
    public async Task An_enum_is_stored_by_name_not_by_number()
    {
        // So that inserting a member in the middle of the enum does not silently change what old rows mean.
        var created = await Widgets.Create(AWidget(status: WidgetStatus.Retired));

        Assert.Equal("Retired", await ScalarText($"SELECT status FROM widgets WHERE id = {created.Id}"));
    }

    [Fact]
    public async Task Create_many_writes_all_of_them()
    {
        var ids = await Widgets.Create([AWidget("One"), AWidget("Two"), AWidget("Three")]);

        Assert.Equal(3, ids.Count);
        Assert.Equal(3, await Widgets.Count(new WidgetFilter()));
    }

    [Fact]
    public async Task Get_by_id_returns_null_when_there_is_no_such_row()
    {
        Assert.Null(await Widgets.Get(404L));
    }

    [Fact]
    public async Task Get_by_ids_returns_the_ones_that_exist()
    {
        var first = await Widgets.Create(AWidget("One"));
        var second = await Widgets.Create(AWidget("Two"));

        var found = await Widgets.Get([first.Id, second.Id, 404L]);

        // Missing keys are skipped rather than throwing, so the result can be shorter than the input.
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public async Task Get_by_no_ids_asks_the_database_nothing()
    {
        Assert.Empty(await Widgets.Get(Array.Empty<long>()));
    }

    [Fact]
    public async Task A_filter_of_nothing_matches_everything()
    {
        await Widgets.Create(AWidget("One"));
        await Widgets.Create(AWidget("Two"));

        Assert.Equal(2, (await Widgets.Get(new WidgetFilter())).Count);
    }

    [Fact]
    public async Task Filtering_by_a_string_matches_exactly()
    {
        await Widgets.Create(AWidget("Sprocket"));
        await Widgets.Create(AWidget("Cog"));

        var found = await Widgets.Get(new WidgetFilter { Name = "Sprocket" });

        Assert.Equal("Sprocket", Assert.Single(found).Name);
    }

    [Fact]
    public async Task Filtering_by_a_substring_becomes_a_LIKE()
    {
        await Widgets.Create(AWidget("One", "contains the needle here"));
        await Widgets.Create(AWidget("Two", "does not"));

        var found = await Widgets.Get(new WidgetFilter { DescriptionContains = "needle" });

        Assert.Equal("One", Assert.Single(found).Name);
    }

    [Fact]
    public async Task Filtering_by_an_enum_compares_the_stored_name()
    {
        await Widgets.Create(AWidget("Live", status: WidgetStatus.Active));
        await Widgets.Create(AWidget("Old", status: WidgetStatus.Retired));

        var found = await Widgets.Get(new WidgetFilter { Status = WidgetStatus.Active });

        Assert.Equal("Live", Assert.Single(found).Name);
    }

    [Fact]
    public async Task Filtering_by_a_number_compares_in_the_database()
    {
        await Widgets.Create(AWidget("Few", quantity: 1));
        await Widgets.Create(AWidget("Many", quantity: 99));

        var found = await Widgets.Get(new WidgetFilter { MinimumQuantity = 50 });

        Assert.Equal("Many", Assert.Single(found).Name);
    }

    [Fact]
    public async Task Filters_combine_rather_than_replace_each_other()
    {
        await Widgets.Create(AWidget("Match", quantity: 99, status: WidgetStatus.Active));
        await Widgets.Create(AWidget("Match", quantity: 1, status: WidgetStatus.Active));
        await Widgets.Create(AWidget("Match", quantity: 99, status: WidgetStatus.Retired));

        var found = await Widgets.Get(new WidgetFilter
        {
            Name = "Match",
            MinimumQuantity = 50,
            Status = WidgetStatus.Active,
        });

        Assert.Equal(99, Assert.Single(found).Quantity);
    }

    [Fact]
    public async Task GetKeys_returns_ids_without_loading_rows()
    {
        var created = await Widgets.Create(AWidget("Only"));

        Assert.Equal(created.Id, Assert.Single(await Widgets.GetKeys(new WidgetFilter())));
    }

    [Fact]
    public async Task Count_respects_the_filter()
    {
        await Widgets.Create(AWidget(status: WidgetStatus.Active));
        await Widgets.Create(AWidget(status: WidgetStatus.Retired));

        Assert.Equal(1, await Widgets.Count(new WidgetFilter { Status = WidgetStatus.Active }));
    }

    [Fact]
    public async Task Update_by_key_changes_the_row()
    {
        var created = await Widgets.Create(AWidget("Before", quantity: 1));

        var updated = await Widgets.Update(created.Id, AWidget("After", quantity: 2, status: WidgetStatus.Active));

        Assert.Equal("After", updated.Name);
        Assert.Equal(2, (await Widgets.Get(created.Id))!.Quantity);
    }

    [Fact]
    public async Task Update_moves_the_updated_timestamp_and_leaves_created_alone()
    {
        Clock.SetTime(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var created = await Widgets.Create(AWidget());

        Clock.Advance(TimeSpan.FromDays(1));
        var updated = await Widgets.Update(created.Id, AWidget("Changed"));

        Assert.Equal(created.Created, updated.Created);
        Assert.Equal(Clock.Now(), updated.Updated);
    }

    [Fact]
    public async Task Update_of_a_missing_row_throws_rather_than_inserting_one()
    {
        await Assert.ThrowsAsync<NotFoundIdException>(() => Widgets.Update(404L, AWidget()));
        Assert.Equal(0, await Widgets.Count(new WidgetFilter()));
    }

    [Fact]
    public async Task Update_by_entity_writes_the_whole_thing_back()
    {
        var created = await Widgets.Create(AWidget("Before"));

        var updated = await Widgets.Update(created with { Name = "After" });

        Assert.Equal("After", updated.Name);
    }

    [Fact]
    public async Task Update_many_applies_every_edit()
    {
        var first = await Widgets.Create(AWidget("One"));
        var second = await Widgets.Create(AWidget("Two"));

        var changed = await Widgets.Update(new Dictionary<long, WidgetEdit>
        {
            [first.Id] = AWidget("One changed"),
            [second.Id] = AWidget("Two changed"),
        });

        Assert.Equal(2, changed);
        Assert.Equal("One changed", (await Widgets.Get(first.Id))!.Name);
    }

    [Fact]
    public async Task Update_many_writes_nothing_when_any_key_is_missing()
    {
        var existing = await Widgets.Create(AWidget("Untouched"));

        await Assert.ThrowsAsync<NotFoundIdException>(() => Widgets.Update(new Dictionary<long, WidgetEdit>
        {
            [existing.Id] = AWidget("Changed"),
            [404L] = AWidget("Nowhere"),
        }));

        // The point of the throw: a partial write would leave the caller believing all of it landed.
        Assert.Equal("Untouched", (await Widgets.Get(existing.Id))!.Name);
    }

    [Fact]
    public async Task Update_many_with_nothing_to_do_is_not_an_error()
    {
        Assert.Equal(0, await Widgets.Update(new Dictionary<long, WidgetEdit>()));
    }

    [Fact]
    public async Task Delete_removes_the_row()
    {
        var created = await Widgets.Create(AWidget());

        Assert.Equal(1, await Widgets.Delete(created.Id));
        Assert.Null(await Widgets.Get(created.Id));
    }

    [Fact]
    public async Task Delete_of_a_missing_row_throws()
    {
        await Assert.ThrowsAsync<NotFoundIdException>(() => Widgets.Delete(404L));
    }

    [Fact]
    public async Task Delete_many_removes_all_of_them()
    {
        var first = await Widgets.Create(AWidget());
        var second = await Widgets.Create(AWidget());

        Assert.Equal(2, await Widgets.Delete([first.Id, second.Id]));
        Assert.Equal(0, await Widgets.Count(new WidgetFilter()));
    }

    [Fact]
    public async Task Delete_many_removes_nothing_when_any_key_is_missing()
    {
        var existing = await Widgets.Create(AWidget());

        await Assert.ThrowsAsync<NotFoundIdException>(() => Widgets.Delete([existing.Id, 404L]));
        Assert.NotNull(await Widgets.Get(existing.Id));
    }

    [Fact]
    public async Task Delete_by_filter_deletes_in_the_database()
    {
        await Widgets.Create(AWidget("Keep", status: WidgetStatus.Active));
        await Widgets.Create(AWidget("Drop", status: WidgetStatus.Retired));

        Assert.Equal(1, await Widgets.Delete(new WidgetFilter { Status = WidgetStatus.Retired }));
        Assert.Equal("Keep", Assert.Single(await Widgets.Get(new WidgetFilter())).Name);
    }

    [Fact]
    public async Task One_test_cannot_see_another_ones_rows()
    {
        // The base class truncates between tests. Without that, every count assertion above is a lie.
        Assert.Equal(0, await Widgets.Count(new WidgetFilter()));
    }
}
