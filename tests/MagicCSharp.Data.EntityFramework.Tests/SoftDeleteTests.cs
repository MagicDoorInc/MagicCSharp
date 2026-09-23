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
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 5, 4, 0, 0, 0, TimeSpan.Zero));
        var contact = await DeletableContacts.Create(AContact());

        var deleted = await DeletableContacts.SoftDelete(contact.Id);

        Assert.Equal(TimeProvider.GetUtcNow(), deleted.Deleted);
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

        Assert.Single(await DeletableContacts.Get(new ContactFilter { ShouldIncludeDeleted = true }));
    }

    [Fact]
    public async Task Soft_deleting_a_row_that_is_not_there_throws()
    {
        await Assert.ThrowsAsync<MagicCSharp.Infrastructure.Exceptions.NotFoundIdException>(
            () => DeletableContacts.SoftDelete(404L));
    }
}
