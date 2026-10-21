using EleFi.Application.Abstractions;
using EleFi.Application.Receipts;
using EleFi.Application.Tests.Support;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Domain.Receipts;
using EleFi.Domain.Transactions;
using NSubstitute;

namespace EleFi.Application.Tests.Receipts;

/// <summary>Attaching, recording, settling and correcting from receipts.</summary>
public class ReceiptServiceTests
{
    private static readonly ScannedReceipt Photo = new(["Gnani Mart", "Total 500.00"], [1, 2, 3], [9]);

    private readonly InMemoryLedger _ledger = new();
    private readonly InMemoryReceipts _receipts = new();
    private readonly MovableClock _clock = new();

    [Fact]
    public async Task A_receipt_that_agrees_is_attached_with_its_photo_and_the_flag_left_alone()
    {
        var paid = await SpendAsync("Gnani Mart", 500_00);

        var outcome = await Service().AttachAsync(paid.Id, Reading("GNANI MART", 500_00), Photo, []);

        Assert.True(outcome.Succeeded);
        Assert.Equal(Discrepancy.None, outcome.Discrepancy);
        Assert.False(paid.NeedsReview);
        var receipt = Assert.Single(_receipts.Rows);
        Assert.Equal(paid.Id, receipt.TransactionId);
        Assert.Equal([1, 2, 3], Assert.Single(_receipts.Images).Photo);
    }

    [Fact]
    public async Task A_receipt_that_disagrees_flags_the_transaction_for_review_and_says_on_what()
    {
        var paid = await SpendAsync("Sri Sundha Mobiles", 300_00);

        var outcome = await Service().AttachAsync(paid.Id, Reading("Sri Sundha Mobiles", 450_00), Photo, []);

        Assert.Equal(Discrepancy.Amount, outcome.Discrepancy);
        Assert.True(paid.NeedsReview);
    }

    [Fact]
    public async Task The_items_read_from_a_shop_receipt_can_become_the_transactions_items()
    {
        var paid = await SpendAsync("Gnani Mart", 500_00);

        await Service().AttachAsync(paid.Id, Reading("Gnani Mart", 500_00), Photo, [new ReadItem("Rice", 450_00)]);

        Assert.Equal(["Rice", null], paid.Items.Select(i => i.Name));
        Assert.Equal([450_00, 50_00], paid.Items.Select(i => i.AmountMinor));
    }

    [Fact]
    public async Task Items_that_come_to_more_than_the_transaction_are_refused_and_nothing_is_attached()
    {
        var paid = await SpendAsync("Gnani Mart", 500_00);

        var outcome = await Service().AttachAsync(paid.Id, Reading("Gnani Mart", 500_00), Photo, [new ReadItem("Rice", 600_00)]);

        Assert.False(outcome.Succeeded);
        Assert.Empty(_receipts.Rows);
        Assert.Empty(paid.Items);
    }

    [Fact]
    public async Task A_receipt_that_matches_nothing_is_recorded_as_a_new_transaction_for_review_from_its_card()
    {
        _ledger.AddContainer("SBI", ContainerKind.BankAccount);
        var card = _ledger.AddContainer("HDFC Card", ContainerKind.CreditCard, last4: "4417");
        var reading = Reading("Chai Point", 100_00) with { Facts = new ReceiptFacts("Chai Point", 100_00, _clock.Today, "4417") };

        var outcome = await Service().RecordAsync(reading, Photo, []);

        Assert.True(outcome.Succeeded, outcome.Error);
        var recorded = outcome.Transaction!;
        Assert.True(recorded.NeedsReview);
        Assert.Equal(CaptureSource.ShareSheet, recorded.CaptureSource);
        Assert.Equal(_ledger.PartyOf(card).Id, recorded.SourcePartyId);
        Assert.Equal("Chai Point", recorded.DestinationParty!.Name);
        Assert.Equal(recorded.Id, Assert.Single(_receipts.Rows).TransactionId);
    }

    [Fact]
    public async Task Settling_keeps_the_transaction_and_the_receipt_stops_asking_to_be_checked()
    {
        var paid = await SpendAsync("Gnani Mart", 500_00);
        var service = Service();
        await service.AttachAsync(paid.Id, Reading("Gnani Mart", 520_00), Photo, []);

        Assert.True(Assert.Single(await service.ForTransactionAsync(paid.Id)).NeedsChecking);

        await service.SettleAsync(_receipts.Rows[0].Id);

        var view = Assert.Single(await service.ForTransactionAsync(paid.Id));
        Assert.False(view.NeedsChecking);
        Assert.Equal(Discrepancy.Amount, view.Discrepancy);
        Assert.Equal([9], view.Thumbnail);
    }

    [Fact]
    public async Task Using_the_receipts_amount_corrects_the_transaction_and_the_discrepancy_goes()
    {
        var paid = await SpendAsync("Gnani Mart", 500_00);
        var service = Service();
        await service.AttachAsync(paid.Id, Reading("Gnani Mart", 520_00), Photo, []);

        var result = await service.UseAmountAsync(_receipts.Rows[0].Id);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(520_00, paid.SourceAmountMinor);
        Assert.Equal(Discrepancy.None, Assert.Single(await service.ForTransactionAsync(paid.Id)).Discrepancy);
    }

    [Fact]
    public async Task Removing_a_receipt_hides_it_from_its_transaction()
    {
        var paid = await SpendAsync("Gnani Mart", 500_00);
        var service = Service();
        await service.AttachAsync(paid.Id, Reading("Gnani Mart", 500_00), Photo, []);

        await service.RemoveAsync(_receipts.Rows[0].Id);

        Assert.Empty(await service.ForTransactionAsync(paid.Id));
    }

    [Fact]
    public async Task The_matcher_finds_the_payment_a_receipt_belongs_to()
    {
        var paid = await SpendAsync("Zomato", 312_00);
        await SpendAsync("Swiggy", 250_00);
        var matcher = new ReceiptMatcher(_ledger.Transactions, _clock);

        var reading = matcher.Read(new ScannedReceipt(["Paid to", "Zomato Ltd", "₹312", "UPI Ref 123456789012"], null, null))!;

        Assert.Equal(paid.Id, (await matcher.BestAsync(reading))!.Id);
    }

    private ReceiptService Service()
    {
        var contacts = Substitute.For<IContactRepository>();
        contacts.AliasesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var capture = new CaptureService(_ledger.Transactions, _ledger.Parties, _ledger.Apps, _ledger.Labels, _clock);
        var editing = new EditTransactionService(_ledger.Transactions, _ledger.Parties, _ledger.Labels, Substitute.For<IAuditRepository>(), _clock, _ledger.Apps);
        return new ReceiptService(_receipts, _ledger.Transactions, _ledger.Containers, _ledger.Parties, contacts, capture, editing, _clock);
    }

    private ReceiptReading Reading(string payee, long minor) =>
        new(ReceiptKind.Shop, new ReceiptFacts(payee, minor, _clock.Today, null));

    private async Task<Transaction> SpendAsync(string payee, long minor)
    {
        var bank = _ledger.ContainerRows.FirstOrDefault() ?? _ledger.AddContainer("SBI", ContainerKind.BankAccount);
        var shop = await _ledger.Parties.GetOrCreateExternalAsync(payee);
        var result = await new CaptureService(_ledger.Transactions, _ledger.Parties, _ledger.Apps, _ledger.Labels, _clock)
            .CaptureAsync(new CaptureRequest(_ledger.PartyOf(bank).Id, shop.Id, minor, "INR", minor, "INR", _clock.Today));

        Assert.True(result.Succeeded, result.Error);
        return result.Transaction!;
    }
}
