using EleFi.Application.Abstractions;
using EleFi.Application.Suggestions;
using EleFi.Application.Tests.Support;
using EleFi.Application.Transactions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Apps;
using EleFi.Domain.Containers;
using EleFi.Domain.Labels;
using EleFi.Domain.Money;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;
using NSubstitute;

namespace EleFi.Application.Tests.Suggestions;

/// <summary>
/// From an alert to a suggestion, and from a confirmed suggestion to a transaction.
/// </summary>
public class SuggestionServiceTests
{
    private const string HdfcSms = "Rs.450.00 debited from a/c XX4417 on 01-09-26 to VPA zomato@hdfcbank";
    private static readonly string GPay = PaymentApps.GPay.Package;

    private readonly InMemorySuggestions _suggestions = new();
    private readonly MovableClock _clock = new();
    private readonly Container _card = new() { Name = "HDFC Card", Kind = ContainerKind.CreditCard, AccountNumberLast4 = "4417" };
    private readonly Party _cardParty = new() { Kind = PartyKind.Container };
    private readonly List<Transaction> _recorded = [];
    private readonly App _gpayApp = new() { Name = "GPay" };

    [Fact]
    public async Task SM2_ingesting_an_alert_records_no_transaction()
    {
        var service = Service();

        var result = await service.IngestAsync("VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr);

        Assert.True(result.Created);
        Assert.Empty(_recorded);
    }

    [Fact]
    public async Task SM11_the_card_is_matched_by_its_last_four_digits()
    {
        var service = Service();

        var result = await service.IngestAsync("VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr);

        Assert.Equal(_card.Id, result.Suggestion!.ContainerId);
    }

    [Fact]
    public async Task The_app_notification_completes_the_sms_suggestion_instead_of_adding_a_second()
    {
        var service = Service();

        await service.IngestAsync("VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr);
        var app = await service.IngestAsync(
            AlertChannel.PaymentApp, GPay, "Paid ₹450 to Zomato for Lunch", _clock.UtcNow.AddSeconds(30), Currency.Inr);

        Assert.True(app.Corroborated);

        var only = Assert.Single(_suggestions.Rows);
        Assert.Equal(_card.Id, only.ContainerId);
        Assert.Equal("Zomato", only.CounterpartyText);
        Assert.Equal("Lunch", only.Note);
        Assert.Equal("GPay", only.PaymentAppName);
        Assert.Equal(AlertEvidence.Sms | AlertEvidence.PaymentApp, only.Evidence);
        Assert.Equal(1, _suggestions.Updates);
    }

    [Fact]
    public async Task The_sms_arriving_second_still_completes_the_app_suggestion()
    {
        var service = Service();

        await service.IngestAsync(AlertChannel.PaymentApp, GPay, "Paid ₹450 to Zomato", _clock.UtcNow, Currency.Inr);
        await service.IngestAsync("VM-HDFCBK", HdfcSms, _clock.UtcNow.AddMinutes(2), Currency.Inr);

        var only = Assert.Single(_suggestions.Rows);
        Assert.Equal(_card.Id, only.ContainerId);
        Assert.Equal("Zomato", only.CounterpartyText);
    }

    [Fact]
    public async Task SM6_a_reposted_notification_after_merging_is_still_a_duplicate()
    {
        var service = Service();
        var at = _clock.UtcNow;

        await service.IngestAsync("VM-HDFCBK", HdfcSms, at, Currency.Inr);
        await service.IngestAsync(AlertChannel.PaymentApp, GPay, "Paid ₹450 to Zomato", at.AddSeconds(20), Currency.Inr);

        // Apps re-post a notification when it is updated. The second copy must not become a
        // fresh suggestion just because the first was folded into the SMS one.
        var again = await service.IngestAsync(
            AlertChannel.PaymentApp, GPay, "Paid ₹450 to Zomato", at.AddSeconds(25), Currency.Inr);

        Assert.False(again.Created);
        Assert.Single(_suggestions.Rows);
    }

    [Fact]
    public async Task A_payment_app_notification_alone_becomes_a_suggestion_with_no_container()
    {
        var service = Service();

        var result = await service.IngestAsync(
            AlertChannel.PaymentApp, GPay, "Rahul paid you ₹500", _clock.UtcNow, Currency.Inr);

        var suggestion = result.Suggestion!;
        Assert.Equal(TransactionKind.Credit, suggestion.Direction);
        Assert.Equal("Rahul", suggestion.CounterpartyText);

        // SM11: an app never says which account, so the user is asked rather than guessed for.
        Assert.Null(suggestion.ContainerId);
    }

    [Fact]
    public async Task An_unrecognised_alert_leaves_nothing_behind()
    {
        var service = Service();

        var result = await service.IngestAsync(
            AlertChannel.PaymentApp, GPay, "Get ₹50 cashback on your next recharge", _clock.UtcNow, Currency.Inr);

        Assert.False(result.Created);
        Assert.Empty(_suggestions.Rows);
    }

    [Fact]
    public async Task Confirming_records_the_payment_app_and_the_note_on_the_transaction()
    {
        var service = Service();
        await service.IngestAsync("VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr);
        var merged = (await service.IngestAsync(
            AlertChannel.PaymentApp, GPay, "Paid ₹450 to Zomato for Lunch", _clock.UtcNow, Currency.Inr)).Suggestion!;

        var result = await service.ConfirmAsync(merged.Id, _card.Id, "Zomato");

        Assert.True(result.Succeeded, result.Error);
        var transaction = Assert.Single(_recorded);
        Assert.Equal(_gpayApp.Id, transaction.PaymentAppId);
        Assert.Equal("Lunch", transaction.Description);
        Assert.Equal(45000, transaction.SourceAmountMinor);

        // SM9: both channels described it, and the bank's message names the account.
        Assert.Equal(CaptureSource.Sms, transaction.CaptureSource);
        Assert.Equal(SuggestionState.Confirmed, merged.State);
    }

    [Fact]
    public async Task Confirming_an_app_only_suggestion_says_where_it_came_from()
    {
        var service = Service();
        var suggestion = (await service.IngestAsync(
            AlertChannel.PaymentApp, GPay, "Paid ₹450 to Zomato", _clock.UtcNow, Currency.Inr)).Suggestion!;

        await service.ConfirmAsync(suggestion.Id, _card.Id, "Zomato");

        var transaction = Assert.Single(_recorded);
        Assert.Equal(CaptureSource.PaymentApp, transaction.CaptureSource);

        // The chosen name already says "Zomato", so repeating it as a note would be noise.
        Assert.Null(transaction.Description);
    }

    [Fact]
    public async Task SM7_a_suggestion_cannot_be_confirmed_twice()
    {
        var service = Service();
        var suggestion = (await service.IngestAsync("VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr)).Suggestion!;

        await service.ConfirmAsync(suggestion.Id, _card.Id, "Zomato");
        var second = await service.ConfirmAsync(suggestion.Id, _card.Id, "Zomato");

        Assert.False(second.Succeeded);
        Assert.Single(_recorded);
    }

    [Fact]
    public async Task A_bank_sms_for_a_payment_already_added_from_the_app_is_not_offered_again()
    {
        var service = Service();
        var fromApp = (await service.IngestAsync(
            AlertChannel.PaymentApp, GPay, "Paid ₹450 to Zomato", _clock.UtcNow, Currency.Inr)).Suggestion!;
        await service.ConfirmAsync(fromApp.Id, _card.Id, "Zomato");

        var lateSms = await service.IngestAsync("VM-HDFCBK", HdfcSms, _clock.UtcNow.AddMinutes(2), Currency.Inr);

        Assert.False(lateSms.Created);
        Assert.Single(_suggestions.Rows);
        Assert.Single(_recorded);
    }

    [Fact]
    public async Task The_apps_time_reaches_the_transaction_but_an_sms_time_never_does()
    {
        var service = Service();
        var at = new DateTimeOffset(2026, 9, 1, 7, 34, 0, TimeSpan.Zero);

        var fromApp = (await service.IngestAsync(AlertChannel.PaymentApp, GPay, "Paid ₹450 to Zomato", at, Currency.Inr)).Suggestion!;
        var fromSms = (await service.IngestAsync("VM-HDFCBK", "Rs.99.00 debited from a/c XX4417 on 01-09-26 to BLINKIT", at, Currency.Inr)).Suggestion!;

        Assert.Equal(TimeOnly.FromDateTime(at.ToLocalTime().DateTime), fromApp.OccurredAtTime);
        Assert.Null(fromSms.OccurredAtTime);

        await service.ConfirmAsync(fromApp.Id, _card.Id, "Zomato");
        Assert.Equal(fromApp.OccurredAtTime, _recorded[0].OccurredAtTime);
    }

    [Fact]
    public async Task D1_a_card_bill_is_confirmed_as_a_self_transfer_to_the_chosen_card()
    {
        var bank = new Container { Name = "SBI", Kind = ContainerKind.BankAccount };
        var bankParty = new Party { Kind = PartyKind.Container };
        var service = Service((bank, bankParty));

        var bill = (await service.IngestAsync(
            AlertChannel.PaymentApp, PaymentApps.Cred.Package, "Paid ₹5,000 to HDFC Credit Card", _clock.UtcNow, Currency.Inr)).Suggestion!;

        var refused = await service.ConfirmAsync(bill.Id, bank.Id, string.Empty);
        Assert.False(refused.Succeeded);

        var result = await service.ConfirmAsync(bill.Id, bank.Id, string.Empty, destinationContainerId: _card.Id);

        Assert.True(result.Succeeded, result.Error);
        var transfer = Assert.Single(_recorded);
        Assert.Equal(bankParty.Id, transfer.SourcePartyId);
        Assert.Equal(_cardParty.Id, transfer.DestinationPartyId);
    }

    [Fact]
    public async Task Confirming_without_a_payee_is_refused_rather_than_inventing_one()
    {
        var service = Service();
        var suggestion = (await service.IngestAsync("VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr)).Suggestion!;

        var result = await service.ConfirmAsync(suggestion.Id, _card.Id, "  ");

        Assert.False(result.Succeeded);
        Assert.Empty(_recorded);
    }

    [Fact]
    public async Task SM5_dismissing_destroys_the_suggestion()
    {
        var service = Service();
        var suggestion = (await service.IngestAsync("VM-HDFCBK", HdfcSms, _clock.UtcNow, Currency.Inr)).Suggestion!;

        await service.DismissAsync(suggestion.Id);

        Assert.Empty(_suggestions.Rows);
        Assert.Equal(0, await service.PendingCountAsync());
    }

    private SuggestionService Service((Container Container, Party Party)? another = null)
    {
        var containers = Substitute.For<IContainerRepository>();
        containers.FindByLast4Async("4417", Arg.Any<CancellationToken>()).Returns([_card]);
        containers.PartyForAsync(_card.Id, Arg.Any<CancellationToken>()).Returns(_cardParty);
        if (another is { } extra)
        {
            containers.PartyForAsync(extra.Container.Id, Arg.Any<CancellationToken>()).Returns(extra.Party);
        }

        var known = new Dictionary<string, Party>(StringComparer.OrdinalIgnoreCase);
        var parties = Substitute.For<IPartyRepository>();
        parties.GetOrCreateExternalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var name = call.Arg<string>();
            if (!known.TryGetValue(name, out var party))
            {
                party = new Party { Kind = PartyKind.External, Name = name };
                known[name] = party;
            }

            return party;
        });
        parties.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var id = call.Arg<Guid>();
            if (id == _cardParty.Id)
            {
                return _cardParty;
            }

            return another is { } extra && extra.Party.Id == id
                ? extra.Party
                : known.Values.FirstOrDefault(p => p.Id == id);
        });

        var apps = Substitute.For<IAppRepository>();
        apps.GetOrCreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            call.Arg<string>() == "GPay" ? _gpayApp : new App { Name = call.Arg<string>() });

        var labels = Substitute.For<ILabelRepository>();
        labels.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Label>());

        var transactions = Substitute.For<ITransactionRepository>();
        transactions
            .When(t => t.AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>()))
            .Do(call => _recorded.Add(call.Arg<Transaction>()));

        var capture = new CaptureService(transactions, parties, apps, labels, _clock);
        return new SuggestionService(_suggestions, containers, parties, apps, capture, _clock);
    }
}
