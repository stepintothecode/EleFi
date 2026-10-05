using EleFi.Application.Suggestions;
using EleFi.Application.Tests.Support;
using EleFi.Application.Transactions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Containers;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;
using NSubstitute;

namespace EleFi.Application.Tests.Suggestions;

/// <summary>
/// ADR-0015: an alert is recorded straight away as a transaction flagged Needs Review, and a
/// second alert about the same payment completes it rather than adding another.
/// </summary>
public class AlertCaptureServiceTests
{
    private const string HdfcSms = "Rs.450.00 debited from a/c XX4417 on 01-09-26 to VPA zomato@hdfcbank";
    private static readonly string GPay = PaymentApps.GPay.Package;

    private readonly InMemoryLedger _ledger = new();
    private readonly InMemorySuggestions _links = new();
    private readonly MovableClock _clock = new();
    private readonly Container _card;
    private readonly Container _bank;

    public AlertCaptureServiceTests()
    {
        _bank = _ledger.AddContainer("SBI", ContainerKind.BankAccount, "9001");
        _card = _ledger.AddContainer("HDFC Card", ContainerKind.CreditCard, "4417", "HDFC Bank");
    }

    [Fact]
    public async Task ADR_0015_a_bank_sms_is_recorded_at_once_and_flagged_for_review()
    {
        var result = await Service().IngestAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr);

        var transaction = Assert.Single(_ledger.TransactionRows);
        Assert.Same(transaction, result.Transaction);
        Assert.True(transaction.NeedsReview);
        Assert.Equal(CaptureSource.Sms, transaction.CaptureSource);
        Assert.Equal(45000, transaction.SourceAmountMinor);
        Assert.Equal(TransactionKind.Debit, transaction.Kind);
    }

    [Fact]
    public async Task SM11_the_card_named_by_its_last_four_digits_is_the_one_charged()
    {
        await Service().IngestAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr);

        Assert.Equal(_ledger.PartyOf(_card).Id, _ledger.TransactionRows[0].SourcePartyId);
    }

    [Fact]
    public async Task The_app_notification_completes_the_sms_transaction_instead_of_adding_a_second()
    {
        var service = Service();

        await service.IngestAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr);
        var app = await service.IngestAsync(
            AlertChannel.PaymentApp, GPay, "Paid ₹450 to Zomato for Lunch", _clock.UtcNow.AddSeconds(30), Currency.Inr);

        Assert.True(app.Merged);
        var transaction = Assert.Single(_ledger.TransactionRows);

        // The bank's card, the app's payee, note, app and time.
        Assert.Equal(_ledger.PartyOf(_card).Id, transaction.SourcePartyId);
        Assert.Equal("Zomato", transaction.DestinationParty!.Name);
        Assert.Equal("Lunch", transaction.Description);
        Assert.Equal("GPay", transaction.PaymentApp!.Name);
        Assert.NotNull(transaction.OccurredAtTime);
        Assert.True(transaction.NeedsReview);
    }

    [Fact]
    public async Task The_sms_arriving_second_moves_a_guessed_container_to_the_real_card()
    {
        var amex = _ledger.AddContainer("Amex", ContainerKind.CreditCard);
        _ledger.ContainerRows.Remove(amex);
        _ledger.ContainerRows.Insert(0, amex);
        var service = Service();

        await service.IngestAsync(AlertChannel.PaymentApp, GPay, "Paid ₹450 to Zomato", _clock.UtcNow, Currency.Inr);

        // An app never says which account paid, so the first card in picker order was guessed.
        Assert.Equal(_ledger.PartyOf(amex).Id, _ledger.TransactionRows[0].SourcePartyId);

        await service.IngestAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow.AddMinutes(1), Currency.Inr);

        var transaction = Assert.Single(_ledger.TransactionRows);
        Assert.Equal(_ledger.PartyOf(_card).Id, transaction.SourcePartyId);
        Assert.Equal("Zomato", transaction.DestinationParty!.Name);
    }

    [Fact]
    public async Task A_payment_the_user_already_reviewed_is_left_as_they_left_it()
    {
        var service = Service();
        await service.IngestAsync(AlertChannel.PaymentApp, GPay, "Paid ₹450 to Zomato", _clock.UtcNow, Currency.Inr);
        var reviewed = _ledger.TransactionRows[0];
        reviewed.NeedsReview = false;
        reviewed.Description = "my own words";

        var late = await service.IngestAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow.AddMinutes(1), Currency.Inr);

        Assert.False(late.Recorded);
        Assert.Single(_ledger.TransactionRows);
        Assert.Equal("my own words", reviewed.Description);
    }

    [Fact]
    public async Task SM6_the_same_alert_twice_records_one_transaction()
    {
        var service = Service();

        await service.IngestAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr);
        var again = await service.IngestAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow.AddSeconds(20), Currency.Inr);

        Assert.False(again.Recorded);
        Assert.Single(_ledger.TransactionRows);
    }

    [Fact]
    public async Task D1_a_card_bill_is_recorded_as_a_self_transfer_from_the_bank_to_the_card()
    {
        await Service().IngestAsync(
            AlertChannel.PaymentApp, PaymentApps.Cred.Package, "Paid ₹5,000 to HDFC Credit Card", _clock.UtcNow, Currency.Inr);

        var transaction = Assert.Single(_ledger.TransactionRows);
        Assert.Equal(TransactionKind.SelfTransfer, transaction.Kind);
        Assert.Equal(_ledger.PartyOf(_bank).Id, transaction.SourcePartyId);
        Assert.Equal(_ledger.PartyOf(_card).Id, transaction.DestinationPartyId);
    }

    [Fact]
    public async Task An_unreadable_message_records_nothing()
    {
        var result = await Service().IngestAsync(
            AlertChannel.PaymentApp, GPay, "Get ₹50 cashback on your next recharge", _clock.UtcNow, Currency.Inr);

        Assert.False(result.Recorded);
        Assert.Empty(_ledger.TransactionRows);
        Assert.Empty(_links.Rows);
    }

    [Fact]
    public async Task With_no_containers_at_all_nothing_is_recorded_and_the_reason_says_why()
    {
        _ledger.ContainerRows.Clear();

        var result = await Service().IngestAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr);

        Assert.False(result.Recorded);
        Assert.Contains("container", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reading_a_message_to_show_the_user_records_nothing()
    {
        var parsed = await Service().ParseAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, Currency.Inr);

        Assert.Equal(45000, parsed!.Value.AmountMinor);
        Assert.Empty(_ledger.TransactionRows);
    }

    private AlertCaptureService Service()
    {
        var audit = Substitute.For<EleFi.Application.Abstractions.IAuditRepository>();
        var capture = new CaptureService(_ledger.Transactions, _ledger.Parties, _ledger.Apps, _ledger.Labels, _clock);
        var editing = new EditTransactionService(_ledger.Transactions, _ledger.Parties, _ledger.Labels, audit, _clock);
        var resolver = new AlertPartyResolver(_ledger.Containers, _ledger.Parties, _ledger.Apps, _ledger.Transactions);

        return new AlertCaptureService(_links, resolver, _ledger.Apps, capture, editing, _clock);
    }
}
