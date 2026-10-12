using EleFi.Domain.Notices;
using EleFi.Infrastructure.Persistence.Repositories;
using EleFi.Infrastructure.Tests.Support;

namespace EleFi.Infrastructure.Tests.Persistence.Repositories;

/// <summary>The notification list against a real database.</summary>
public class NoticeRepositoryTests
{
    [Fact]
    public async Task A_second_notice_about_the_same_thing_rewrites_the_first_and_makes_it_unread()
    {
        await using var f = await TestDatabase.CreateAsync();
        var repository = new NoticeRepository(f.Db, f.Clock);
        var first = Make("₹450 to VPA zomato@hdfc", "transactions/1");
        await repository.UpsertAsync(first);
        await repository.SetReadAsync(first.Id, true);

        await repository.UpsertAsync(Make("₹450 to Zomato", "transactions/1"));

        var only = Assert.Single(await repository.ListAsync());
        Assert.Equal("₹450 to Zomato", only.Title);
        Assert.False(only.IsRead);
    }

    [Fact]
    public async Task Read_unread_one_at_a_time_and_all_at_once()
    {
        await using var f = await TestDatabase.CreateAsync();
        var repository = new NoticeRepository(f.Db, f.Clock);
        var one = Make("One", "a");
        await repository.UpsertAsync(one);
        await repository.UpsertAsync(Make("Two", "b"));

        await repository.SetReadAsync(one.Id, true);
        Assert.Equal(1, await repository.UnreadCountAsync());

        await repository.SetAllReadAsync(true);
        Assert.Equal(0, await repository.UnreadCountAsync());

        await repository.SetAllReadAsync(false);
        Assert.Equal(2, await repository.UnreadCountAsync());
    }

    [Fact]
    public async Task Mark_all_read_shows_as_read_when_the_same_screen_lists_them_again()
    {
        await using var f = await TestDatabase.CreateAsync();
        var repository = new NoticeRepository(f.Db, f.Clock);
        await repository.UpsertAsync(Make("One", "a"));
        await repository.UpsertAsync(Make("Two", "b"));

        // The Notifications page lists, marks all, and lists again through one context. A
        // bulk statement that skips the change tracker left the listed rows unread on screen.
        Assert.All(await repository.ListAsync(), n => Assert.False(n.IsRead));

        await repository.SetAllReadAsync(true);
        Assert.All(await repository.ListAsync(), n => Assert.True(n.IsRead));

        await repository.SetAllReadAsync(false);
        Assert.All(await repository.ListAsync(), n => Assert.False(n.IsRead));
    }

    private static Notice Make(string title, string route) => new()
    {
        Title = title,
        Body = "Recorded, needs review.",
        Route = route,
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };
}
