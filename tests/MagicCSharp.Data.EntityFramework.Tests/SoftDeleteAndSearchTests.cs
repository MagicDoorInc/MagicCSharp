using MagicCSharp.Data.EntityFramework.Tests.Fixtures;

namespace MagicCSharp.Data.EntityFramework.Tests;

public class SoftDeleteTests : RepositoryTestBase
{
    [Fact]
    public async Task A_soft_deleted_row_is_still_there()
    {
        var contact = await DeletableContacts.Create(AContact());

        await DeletableContacts.SoftDelete(contact.Id);

        // The row survives; only the filter hides it. That is the whole difference from Delete.
        Assert.NotNull(await ScalarText($"SELECT name FROM contacts WHERE id = {contact.Id}"));
    }

    [Fact]
    public async Task A_soft_deleted_row_is_stamped_with_when()
    {
        Clock.SetTime(new DateTimeOffset(2026, 5, 4, 0, 0, 0, TimeSpan.Zero));
        var contact = await DeletableContacts.Create(AContact());

        var deleted = await DeletableContacts.SoftDelete(contact.Id);

        Assert.Equal(Clock.Now(), deleted.Deleted);
    }

    [Fact]
    public async Task A_soft_deleted_row_is_out_of_the_default_results()
    {
        var contact = await DeletableContacts.Create(AContact());
        await DeletableContacts.SoftDelete(contact.Id);

        Assert.Empty(await DeletableContacts.Get(new ContactFilter()));
        Assert.Equal(0, await DeletableContacts.Count(new ContactFilter()));
    }

    [Fact]
    public async Task A_soft_deleted_row_can_be_asked_for_deliberately()
    {
        var contact = await DeletableContacts.Create(AContact());
        await DeletableContacts.SoftDelete(contact.Id);

        Assert.Single(await DeletableContacts.Get(new ContactFilter { IncludeDeleted = true }));
    }

    [Fact]
    public async Task Soft_deleting_a_row_that_is_not_there_throws()
    {
        await Assert.ThrowsAsync<MagicCSharp.Infrastructure.Exceptions.NotFoundIdException>(
            () => DeletableContacts.SoftDelete(404L));
    }
}

public class SearchTests : RepositoryTestBase
{
    [Fact]
    public async Task A_row_is_found_by_a_stored_keyword()
    {
        var contact = await Contacts.Create(AContact("Jane Smith"));
        await Contacts.SetKeywords(contact.Id, "Jane Smith", "Springfield");

        Assert.Single(await Contacts.Get(new ContactFilter { Search = "jane" }));
    }

    [Fact]
    public async Task Every_word_of_the_query_has_to_match()
    {
        var jane = await Contacts.Create(AContact("Jane Smith"));
        await Contacts.SetKeywords(jane.Id, "Jane Smith");
        var john = await Contacts.Create(AContact("John Smith"));
        await Contacts.SetKeywords(john.Id, "John Smith");

        // More words narrow the result rather than widening it.
        Assert.Equal(2, (await Contacts.Get(new ContactFilter { Search = "smith" })).Count);
        Assert.Single(await Contacts.Get(new ContactFilter { Search = "jane smith" }));
    }

    [Fact]
    public async Task Search_ignores_case_and_punctuation()
    {
        var contact = await Contacts.Create(AContact("Jane Smith"));
        await Contacts.SetKeywords(contact.Id, "Smith, Jane.");

        Assert.Single(await Contacts.Get(new ContactFilter { Search = "JANE" }));
    }

    [Fact]
    public async Task A_non_ascii_name_is_findable_by_that_name()
    {
        // "Søgaard" used to be indexed as "sgaard", so nobody searching the actual name found it. That fix
        // was unit-tested on the string; this is the first time it goes through Postgres.
        var contact = await Contacts.Create(AContact("Kasper Søgaard"));
        await Contacts.SetKeywords(contact.Id, "Søgaard, Kasper");

        Assert.Single(await Contacts.Get(new ContactFilter { Search = "søgaard" }));
    }

    [Fact]
    public async Task Synonyms_rewrite_both_the_stored_value_and_the_query()
    {
        var contact = await Contacts.Create(AContact("Shop"));
        await Contacts.SetKeywords(contact.Id, "12 Baker Street");

        // Stored as "st", so a query for "street" has to be rewritten the same way to match.
        Assert.Single(await Contacts.Get(new ContactFilter { Search = "baker street" }));
        Assert.Single(await Contacts.Get(new ContactFilter { Search = "baker st" }));
    }

    [Fact]
    public async Task A_row_with_no_keywords_matches_no_search()
    {
        await Contacts.Create(AContact("Never indexed"));

        Assert.Empty(await Contacts.Get(new ContactFilter { Search = "never" }));
    }

    [Fact]
    public async Task A_blank_search_does_not_narrow_anything()
    {
        await Contacts.Create(AContact("One"));

        Assert.Single(await Contacts.Get(new ContactFilter { Search = "   " }));
    }

    [Fact]
    public async Task Updating_keywords_replaces_them_rather_than_appending()
    {
        var contact = await Contacts.Create(AContact("Jane"));
        await Contacts.SetKeywords(contact.Id, "Jane");
        await Contacts.SetKeywords(contact.Id, "Roberta");

        Assert.Empty(await Contacts.Get(new ContactFilter { Search = "jane" }));
        Assert.Single(await Contacts.Get(new ContactFilter { Search = "roberta" }));
    }

    [Fact]
    public async Task Reindexing_does_not_count_as_editing_the_entity()
    {
        Clock.SetTime(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var contact = await Contacts.Create(AContact());

        Clock.Advance(TimeSpan.FromDays(1));
        await Contacts.SetKeywords(contact.Id, "anything");

        // The search column is derived from values already saved, so refreshing it is not an edit.
        Assert.Equal(contact.Updated, (await Contacts.Get(contact.Id))!.Updated);
    }
}
