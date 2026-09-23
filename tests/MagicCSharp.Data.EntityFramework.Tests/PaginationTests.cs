using MagicCSharp.Data.EntityFramework.Tests.Fixtures;
using MagicCSharp.Data.Models;

namespace MagicCSharp.Data.EntityFramework.Tests;

public class PaginationTests : RepositoryTestBase
{
    private async Task GivenContacts(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await Contacts.Create(AContact($"Contact {i:00}"));
        }
    }

    [Fact]
    public async Task The_first_page_skips_nothing()
    {
        // Pages are 1-based and Skip is PageSize * (Page - 1). Treating them as 0-based drops the first
        // page of every list, and no unit test of the C# would notice.
        await GivenContacts(5);

        var page = await Contacts.Get(new PaginationRequest(2, 1), new ContactFilter());

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.TotalCount);
    }

    [Fact]
    public async Task Pages_do_not_overlap_and_cover_everything()
    {
        await GivenContacts(5);

        var first = await Contacts.Get(new PaginationRequest(2, 1), new ContactFilter());
        var second = await Contacts.Get(new PaginationRequest(2, 2), new ContactFilter());
        var third = await Contacts.Get(new PaginationRequest(2, 3), new ContactFilter());

        var ids = first.Items.Concat(second.Items).Concat(third.Items).Select(c => c.Id).ToList();

        Assert.Equal(5, ids.Count);
        Assert.Equal(5, ids.Distinct().Count());
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_but_still_reports_the_total()
    {
        await GivenContacts(3);

        var page = await Contacts.Get(new PaginationRequest(10, 9), new ContactFilter());

        Assert.Empty(page.Items);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task The_total_is_the_whole_result_not_the_page()
    {
        await GivenContacts(7);

        var page = await Contacts.Get(new PaginationRequest(2, 1), new ContactFilter());

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(7, page.TotalCount);
        Assert.Equal(4, page.TotalPages);
    }

    [Fact]
    public async Task The_total_counts_only_what_the_filter_matched()
    {
        await Contacts.Create(AContact("In", city: "Springfield"));
        await Contacts.Create(AContact("Out", city: "Shelbyville"));

        var page = await Contacts.Get(new PaginationRequest(10, 1), new ContactFilter { City = "Springfield" });

        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Pagination_can_be_turned_off_to_take_everything()
    {
        await GivenContacts(4);

        var page = await Contacts.Get(new PaginationRequest(disable: true), new ContactFilter());

        Assert.Equal(4, page.Items.Count);
    }
}
