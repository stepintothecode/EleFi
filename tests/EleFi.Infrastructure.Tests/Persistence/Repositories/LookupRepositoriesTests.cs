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
