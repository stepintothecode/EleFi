using EleFi.Application.Containers;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Domain.Transactions;
using EleFi.Infrastructure.Tests.Support;

namespace EleFi.Infrastructure.Tests.Persistence;

/// <summary>A transaction's items show in its own history: added, changed, removed, labelled.</summary>
public class AuditTriggersItemsTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public async Task Adding_resizing_and_removing_items_reads_as_money_in_the_transactions_history()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var id = await SpendAsync(f, 500_00);
        var food = (await f.Labels.ListAsync()).Single(l => l.Name == "Food").Id;

        var milk = ItemDraft.Named("Milk", 200_00) with { LabelIds = [food] };
        await EditAsync(f, id, [milk]);
        await EditAsync(f, id, [milk with { AmountMinor = 150_00 }]);
        await EditAsync(f, id, []);

        var changes = (await f.Editing.HistoryAsync(id)).SelectMany(e => e.Changes).ToList();

        Assert.Contains(new HistoryChange("Item", "nothing", "Milk ₹200.00"), changes);
        Assert.Contains(new HistoryChange("Item", "nothing", "Unnamed ₹300.00"), changes);
        Assert.Contains(new HistoryChange("Item label", "nothing", "Milk: Food"), changes);
        Assert.Contains(new HistoryChange("Item", "Milk ₹200.00", "Milk ₹150.00"), changes);
        Assert.Contains(new HistoryChange("Item", "Unnamed ₹300.00", "Unnamed ₹350.00"), changes);
        Assert.Contains(new HistoryChange("Item", "Milk ₹150.00", "nothing"), changes);
    }

    [Fact]
    public async Task Saving_items_unchanged_writes_nothing_new()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var id = await SpendAsync(f, 500_00);
        await EditAsync(f, id, [ItemDraft.Named("Milk", 200_00)]);
        var before = (await f.Editing.HistoryAsync(id)).Count;

        var current = (await f.Transactions.FindAsync(id))!;
        await f.Editing.EditAsync(EditRequest.Snapshot(current));

        Assert.Equal(before, (await f.Editing.HistoryAsync(id)).Count);
    }

    private static async Task<Guid> SpendAsync(TestDatabase f, long minor)
    {
        var bank = await f.ContainerService.CreateAsync(new CreateContainerRequest(
            "SBI", ContainerKind.BankAccount, "INR", 10_000_00, Today.AddYears(-1)));
        var bankParty = await f.Containers.PartyForAsync(bank.Container!.Id);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");
        var paid = await f.Capture.CaptureAsync(new CaptureRequest(bankParty!.Id, shop.Id, minor, "INR", minor, "INR", Today));
        return paid.Transaction!.Id;
    }

    private static async Task EditAsync(TestDatabase f, Guid id, IReadOnlyList<ItemDraft> items)
    {
        var current = (await f.Transactions.FindAsync(id))!;
        var result = await f.Editing.EditAsync(EditRequest.Snapshot(current) with { Items = items });
        Assert.True(result.Succeeded, result.Error);
    }
}
