using EleFi.Application.Abstractions;
using EleFi.Application.Tests.Support;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Domain.Transactions;
using NSubstitute;

namespace EleFi.Application.Tests.Transactions;

/// <summary>Deleting and getting it back.</summary>
public class EditTransactionServiceTests
{
    private readonly InMemoryLedger _ledger = new();

    [Fact]
    public async Task A_restored_transaction_comes_back_flagged_for_review()
    {
        var spent = await SpendAsync();
        var editing = Service();

        await editing.DeleteAsync(spent.Id);
        Assert.Single(await editing.DeletedAsync());

        await editing.RestoreAsync(spent.Id);

        // It was deleted for a reason, so it is not allowed back looking settled.
        Assert.Null(spent.DeletedAt);
        Assert.True(spent.NeedsReview);
        Assert.Empty(await editing.DeletedAsync());
    }

    [Fact]
    public async Task Restore_all_brings_every_deleted_transaction_back_flagged()
    {
        var first = await SpendAsync();
        var second = await SpendAsync();
        var editing = Service();
        await editing.DeleteAsync(first.Id);
        await editing.DeleteAsync(second.Id);

        Assert.Equal(2, await editing.RestoreAllAsync());

        Assert.All([first, second], t =>
        {
            Assert.Null(t.DeletedAt);
            Assert.True(t.NeedsReview);
        });
    }

    private EditTransactionService Service() =>
        new(_ledger.Transactions, _ledger.Parties, _ledger.Labels, Substitute.For<IAuditRepository>(), new MovableClock());

    private async Task<Transaction> SpendAsync()
    {
        var bank = _ledger.ContainerRows.FirstOrDefault() ?? _ledger.AddContainer("SBI", ContainerKind.BankAccount);
        var shop = await _ledger.Parties.GetOrCreateExternalAsync("Shop");
        var clock = new MovableClock();

        var result = await new CaptureService(_ledger.Transactions, _ledger.Parties, _ledger.Apps, _ledger.Labels, clock)
            .CaptureAsync(new CaptureRequest(_ledger.PartyOf(bank).Id, shop.Id, 100, "INR", 100, "INR", clock.Today));

        Assert.True(result.Succeeded, result.Error);
        return result.Transaction!;
    }
}
