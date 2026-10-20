using EleFi.Application.Containers;
using EleFi.Application.Transactions;
using EleFi.Application.Typeahead;
using EleFi.Domain.Containers;
using EleFi.Infrastructure.Persistence.Repositories;
using EleFi.Infrastructure.Tests.Support;

namespace EleFi.Infrastructure.Tests.Persistence.Repositories;

/// <summary>Notes and apps as used on transactions, for the suggestions on the forms.</summary>
public class SuggestionHistoryRepositoryTests
{
    [Fact]
    public async Task Notes_and_apps_are_counted_per_contact_and_overall()
    {
        await using var f = await TestDatabase.CreateAsync();
        var bank = await f.ContainerService.CreateAsync(new CreateContainerRequest(
            "SBI", ContainerKind.BankAccount, "INR", 10_000_00, new DateOnly(2026, 1, 1)));
        var mine = await f.Containers.PartyForAsync(bank.Container!.Id);
        var mom = await f.Parties.GetOrCreateExternalAsync("Mom");
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");
        var gpay = await f.Apps.GetOrCreateAsync("GPay");
        var cash = await f.Apps.GetOrCreateAsync("Cash");
        var amazon = await f.Apps.GetOrCreateAsync("Amazon");

        async Task RecordAsync(Guid from, Guid to, string? note, Guid? paidWith = null, Guid? boughtThrough = null) =>
            Assert.True((await f.Capture.CaptureAsync(new CaptureRequest(from, to, 100_00, "INR", 100_00, "INR",
                new DateOnly(2026, 8, 1), Description: note, PaymentAppId: paidWith, MarketplaceAppId: boughtThrough))).Succeeded);

        await RecordAsync(mine!.Id, mom.Id, "Gift", cash.Id);
        await RecordAsync(mom.Id, mine.Id, "gift", cash.Id);
        await RecordAsync(mine.Id, shop.Id, "Groceries", gpay.Id, amazon.Id);
        await RecordAsync(mine.Id, shop.Id, "Groceries", gpay.Id);

        var history = new SuggestionHistoryRepository(f.Db);

        var momNotes = await history.NotesAsync("MOM");
        Assert.Equal(2, Assert.Single(momNotes).UsageCount);

        var allNotes = await history.NotesAsync(null);
        Assert.Equal(2, allNotes.Single(n => n.Name == "Groceries").UsageCount);

        Assert.Equal(["Cash"], (await history.AppsAsync(AppSlot.PaidWith, "Mom")).Select(a => a.Name));
        Assert.Equal(2, (await history.AppsAsync(AppSlot.PaidWith, null)).Single(a => a.Name == "GPay").UsageCount);
        Assert.Equal(["Amazon"], (await history.AppsAsync(AppSlot.BoughtThrough, null)).Select(a => a.Name));
    }
}
