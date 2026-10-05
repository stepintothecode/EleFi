using EleFi.Application.Suggestions;
using EleFi.Application.Tests.Support;
using EleFi.Domain.Alerts;
using EleFi.Domain.Containers;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Tests.Suggestions;

/// <summary>Which container a recorded alert lands on when the alert does not say.</summary>
public class AlertPartyResolverTests
{
    private readonly InMemoryLedger _ledger = new();

    [Fact]
    public async Task The_payment_apps_default_container_beats_a_guess()
    {
        _ledger.AddContainer("Amex", ContainerKind.CreditCard);
        var sbi = _ledger.AddContainer("SBI", ContainerKind.BankAccount);
        var gpay = await _ledger.Apps.GetOrCreateAsync("GPay");
        gpay.DefaultContainerId = sbi.Id;

        var ends = await Resolver().ResolveAsync(App("Zomato"));

        Assert.Same(sbi, ends!.Container);
    }

    [Fact]
    public async Task Without_a_default_the_container_last_used_with_that_app_is_chosen()
    {
        _ledger.AddContainer("Amex", ContainerKind.CreditCard);
        var sbi = _ledger.AddContainer("SBI", ContainerKind.BankAccount);
        var gpay = await _ledger.Apps.GetOrCreateAsync("GPay");
        var shop = await _ledger.Parties.GetOrCreateExternalAsync("Shop");
        _ledger.TransactionRows.Add(new Transaction
        {
            SourcePartyId = _ledger.PartyOf(sbi).Id,
            DestinationPartyId = shop.Id,
            SourceAmountMinor = 100,
            DestinationAmountMinor = 100,
            PaymentAppId = gpay.Id,
        });

        var ends = await Resolver().ResolveAsync(App("Zomato"));

        Assert.Same(sbi, ends!.Container);
    }

    [Fact]
    public async Task With_nothing_to_go_on_the_first_container_in_picker_order_is_used()
    {
        _ledger.AddContainer("SBI", ContainerKind.BankAccount);
        var amex = _ledger.AddContainer("Amex", ContainerKind.CreditCard);

        var ends = await Resolver().ResolveAsync(App("Zomato"));

        Assert.Same(amex, ends!.Container);
    }

    [Fact]
    public async Task An_alert_naming_nobody_is_recorded_against_its_app_rather_than_left_blank()
    {
        _ledger.AddContainer("SBI", ContainerKind.BankAccount);

        var ends = await Resolver().ResolveAsync(App(null));

        Assert.Equal("GPay", _ledger.PartyRows.Single(p => p.Id == ends!.DestinationPartyId).Name);
    }

    [Fact]
    public void A_card_bill_lands_on_the_card_its_wording_names()
    {
        var sbi = new Container { Name = "SBI", Kind = ContainerKind.BankAccount };
        var amex = new Container { Name = "Amex Platinum", Kind = ContainerKind.CreditCard };
        var hdfc = new Container { Name = "Regalia", Kind = ContainerKind.CreditCard, InstitutionName = "HDFC Bank" };

        Assert.Same(hdfc, AlertPartyResolver.CardFor("HDFC Credit Card", [amex, hdfc, sbi], sbi));
        Assert.Same(amex, AlertPartyResolver.CardFor("Amex card bill", [amex, hdfc, sbi], sbi));
    }

    [Fact]
    public void A_card_bill_never_lands_on_the_card_it_was_paid_from()
    {
        var only = new Container { Name = "Amex", Kind = ContainerKind.CreditCard };

        Assert.Null(AlertPartyResolver.CardFor("Amex", [only], only));
    }

    [Fact]
    public async Task SM11_a_last_four_shared_by_two_containers_matches_neither()
    {
        _ledger.AddContainer("Card A", ContainerKind.CreditCard, "4417");
        _ledger.AddContainer("Card B", ContainerKind.CreditCard, "4417");

        Assert.Null(await Resolver().MatchLast4Async("4417"));
    }

    private AlertPartyResolver Resolver() =>
        new(_ledger.Containers, _ledger.Parties, _ledger.Apps, _ledger.Transactions);

    private static CaptureSuggestion App(string? payee) => new()
    {
        AmountMinor = 45000,
        Direction = TransactionKind.Debit,
        CounterpartyText = payee,
        PaymentAppName = "GPay",
        Evidence = AlertEvidence.PaymentApp,
    };
}
