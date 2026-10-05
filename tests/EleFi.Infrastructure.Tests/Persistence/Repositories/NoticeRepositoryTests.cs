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

    private static Notice Make(string title, string route) => new()
    {
        Title = title,
        Body = "Recorded, needs review.",
        Route = route,
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };
}
