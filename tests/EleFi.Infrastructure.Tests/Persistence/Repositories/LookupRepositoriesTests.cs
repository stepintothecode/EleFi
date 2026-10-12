using EleFi.Application.Containers;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Infrastructure.Tests.Support;

namespace EleFi.Infrastructure.Tests.Persistence.Repositories;

/// <summary>
/// The lookups behind the pickers: parties, apps, and how often each label is used.
/// </summary>
public class LookupRepositoriesTests
{
    private static readonly DateOnly Today = new(2026, 8, 30);

    [Fact]
    public async Task Party_suggestions_never_include_a_container()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        await AddContainerAsync(f, "HDFC");
        await f.Parties.GetOrCreateExternalAsync("Zomato");

        var suggested = await f.Parties.SuggestAsync(null);

        // A container's Party row has no name, so it used to appear as a blank option.
        Assert.Equal(["Zomato"], suggested.Select(p => p.Name));
    }

    [Fact]
    public async Task Every_external_party_is_listed_however_long_ago_it_was_used()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        await AddContainerAsync(f, "HDFC");

        for (var i = 0; i < 30; i++)
        {
            await f.Parties.GetOrCreateExternalAsync($"Shop {i}");
        }

        var all = await f.Parties.ListExternalAsync();

        Assert.Equal(30, all.Count);
        Assert.All(all, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
    }

    [Fact]
    public async Task Every_app_is_listed()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        await f.Apps.GetOrCreateAsync("GPay");
        await f.Apps.GetOrCreateAsync("Zomato");

        var apps = await f.Apps.ListAsync();

        Assert.Equal(["GPay", "Zomato"], apps.Select(a => a.Name).Order());
    }

    [Fact]
    public async Task Label_usage_counts_each_live_transaction_once()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var bankParty = await AddContainerAsync(f, "HDFC");
        var shop = await f.Parties.GetOrCreateExternalAsync("Big Bazaar");

        var labels = await f.Labels.ListAsync();
        var food = labels.Single(l => l.Name == "Food");
        var travel = labels.Single(l => l.Name == "Travel");

        await SpendAsync(f, bankParty, shop.Id, [food.Id, travel.Id]);
        await SpendAsync(f, bankParty, shop.Id, [food.Id]);

        var counts = await f.Labels.UsageCountsAsync();

        Assert.Equal(2, counts[food.Id]);
        Assert.Equal(1, counts[travel.Id]);
    }

    [Fact]
    public async Task Label_usage_ignores_deleted_transactions_and_omits_unused_labels()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var bankParty = await AddContainerAsync(f, "HDFC");
        var shop = await f.Parties.GetOrCreateExternalAsync("Big Bazaar");
        var food = (await f.Labels.ListAsync()).Single(l => l.Name == "Food");

        var spent = await SpendAsync(f, bankParty, shop.Id, [food.Id]);
        await f.Transactions.SoftDeleteAsync(spent.Transaction!.Id);

        var counts = await f.Labels.UsageCountsAsync();

        // A label used only on something since deleted is not one the user reaches for.
        Assert.Empty(counts);
    }

    [Fact]
    public async Task Names_for_a_history_cover_containers_deleted_parties_and_apps()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var bankParty = await AddContainerAsync(f, "HDFC");
        var gone = await f.Parties.GetOrCreateExternalAsync("Old Shop");
        gone.DeletedAt = f.Clock.UtcNow;
        await f.Db.SaveChangesAsync();
        var app = await f.Apps.GetOrCreateAsync("GPay");
        var stranger = Guid.CreateVersion7();

        var parties = await f.Parties.NamesAsync([bankParty, gone.Id, stranger]);
        var apps = await f.Apps.NamesAsync([app.Id, stranger]);

        // A container's party has no name of its own; the container's is the one people know.
        Assert.Equal("HDFC", parties[bankParty]);

        // Deleted, but the history of a payment to it still has to say who it was.
        Assert.Equal("Old Shop", parties[gone.Id]);
        Assert.False(parties.ContainsKey(stranger));
        Assert.Equal("GPay", apps[app.Id]);
    }

    [Fact]
    public async Task A_deleted_container_comes_back_with_its_party_for_undo()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var party = await AddContainerAsync(f, "Paytm Wallet");
        var container = (await f.Containers.ListAsync()).Single();

        await f.Containers.SoftDeleteAsync(container.Id);
        Assert.Empty(await f.Containers.ListAsync());

        await f.Containers.RestoreAsync(container.Id);

        Assert.Single(await f.Containers.ListAsync());
        Assert.Equal(party, (await f.Containers.PartyForAsync(container.Id))!.Id);
    }

    [Fact]
    public async Task A_deleted_label_comes_back_on_the_transactions_that_carried_it()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var bankParty = await AddContainerAsync(f, "HDFC");
        var shop = await f.Parties.GetOrCreateExternalAsync("Big Bazaar");
        var food = (await f.Labels.ListAsync()).Single(l => l.Name == "Food");
        var spent = await SpendAsync(f, bankParty, shop.Id, [food.Id]);

        var detached = await f.Labels.DeleteAsync(food.Id);
        Assert.Equal([spent.Transaction!.Id], detached);

        await f.Labels.RestoreAsync(food.Id, detached);
        f.Db.ChangeTracker.Clear();

        Assert.Contains(await f.Labels.ListAsync(), l => l.Id == food.Id);
        var again = await f.Transactions.FindAsync(spent.Transaction.Id);
        Assert.Contains(again!.Labels, l => l.LabelId == food.Id);
    }

    private static async Task<Guid> AddContainerAsync(TestDatabase f, string name)
    {
        var result = await f.ContainerService.CreateAsync(
            new CreateContainerRequest(name, ContainerKind.BankAccount, "INR", 10_000_00, Today.AddYears(-1)));

        Assert.True(result.Succeeded, result.Error);
        return (await f.Containers.PartyForAsync(result.Container!.Id))!.Id;
    }

    private static async Task<CaptureResult> SpendAsync(
        TestDatabase f, Guid sourceParty, Guid destinationParty, IReadOnlyList<Guid> labelIds)
    {
        var result = await f.Capture.CaptureAsync(new CaptureRequest(
            sourceParty, destinationParty, 100_00, "INR", 100_00, "INR", Today, LabelIds: labelIds));

        Assert.True(result.Succeeded, result.Error);
        return result;
    }
}
