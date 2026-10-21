using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Receipts;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Domain.Filters;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>A receipt read on the device, matched, checked, and attached or recorded.</summary>
public class ReceiptReviewTests : Bunit.TestContext
{
    private static readonly ScannedReceipt GPayScreen = new(
        ["To SRI SUNDHA MOBILES", "₹450", "30 Aug 2026, 7:12 pm", "UPI transaction ID", "432167890123", "Google Pay"],
        [0xFF, 0xD8, 0x01],
        [0xFF, 0xD8]);

    private static readonly ScannedReceipt ShopReceipt = new(
        ["GNANI MART", "Date: 30/08/2026", "Rice 5kg 1 450.00", "Carry bag 1 5.00", "Grand Total 455.00"], null, null);

    private readonly ITransactionRepository _ledger = Substitute.For<ITransactionRepository>();
    private readonly IPartyRepository _parties = Substitute.For<IPartyRepository>();
    private readonly IContainerRepository _containers = Substitute.For<IContainerRepository>();
    private readonly Fakes.MemoryReceipts _receipts = new();
    private readonly ManualTime _time = new();
    private readonly EleReactions _ele;
    private readonly Container _bank = new() { Name = "SBI", Kind = ContainerKind.BankAccount };
    private readonly Party _bankParty;
    private readonly Transaction _shop;

    public ReceiptReviewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ele = new EleReactions(new Fakes.MemorySettings(), _time);
        Services.AddSingleton(_time);
        _bankParty = new Party { Kind = PartyKind.Container, ContainerId = _bank.Id };
        _shop = Paid("Sri Sundha Mobiles", 300_00);
    }

    [Fact]
    public void A_shared_payment_screen_shows_what_was_read_and_its_match_with_the_difference_in_words()
    {
        var page = Render(GPayScreen, [_shop]);

        page.WaitForAssertion(() => Assert.Equal("Sri Sundha Mobiles", page.Find("#receipt-payee").GetAttribute("value")));
        Assert.Equal("450.00", page.Find("#receipt-amount").GetAttribute("value"));
        Assert.Contains("432167890123", page.Markup, StringComparison.Ordinal);
        Assert.Equal("Amount differs", page.Find(".receipt-match-tag").TextContent);

        // Ele says the details, and stays: a held message, not one that fades in seconds.
        var held = _ele.Current!;
        Assert.Contains("₹450.00 on the receipt, ₹300.00 here", held.Line, StringComparison.Ordinal);
        Services.GetRequiredService<ManualTime>().Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(held.Id, _ele.Current?.Id);
        Assert.NotNull(page.Find("img.receipt-photo"));
    }

    [Fact]
    public void Attaching_keeps_the_receipt_flags_the_transaction_and_ele_says_what_to_check()
    {
        var page = Render(GPayScreen, [_shop]);
        page.WaitForAssertion(() => page.Find(".receipt-match"));

        page.FindAll("button").First(b => b.TextContent.Trim() == "Attach to this one").Click();

        page.WaitForAssertion(() =>
        {
            var receipt = Assert.Single(_receipts.Rows);
            Assert.Equal(_shop.Id, receipt.TransactionId);
            Assert.Equal("432167890123", receipt.Reference);
            Assert.Single(_receipts.Images);
            Assert.True(_shop.NeedsReview);
            Assert.Equal(EleTone.Calm, _ele.Current!.Tone);
            Assert.EndsWith($"transactions/{_shop.Id}", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Opened_from_a_transaction_it_asks_for_the_photo_and_attaches_to_that_one()
    {
        var other = Paid("Gajanana Store", 25_00);
        var scanner = new Fakes.FixedScanner(GPayScreen);
        var page = Render(scanner, [_shop, other], $"receipt?for={other.Id}&from=camera");

        page.WaitForAssertion(() => Assert.Contains("Attaching to", page.Markup, StringComparison.Ordinal));
        Assert.True(scanner.AskedForCamera);
        Assert.Contains("Gajanana Store", page.Find(".receipt-match").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Opened_from_the_wrong_transaction_it_says_which_one_the_receipt_is_for_and_moves_there()
    {
        var gajanana = Paid("Gajanana Store", 25_00);
        var page = Render(new Fakes.FixedScanner(GPayScreen), [gajanana, _shop], $"receipt?for={gajanana.Id}&from=gallery");

        // Ele says which transaction it is for, and holds the way there in her bubble.
        page.WaitForAssertion(() => Assert.Contains("Sri Sundha Mobiles", _ele.Current?.Line ?? string.Empty, StringComparison.Ordinal));
        var attachThere = Assert.Single(_ele.Current!.Choices);
        Assert.Equal("Attach to Sri Sundha Mobiles", attachThere.Label);

        page.InvokeAsync(attachThere.Run);

        page.WaitForAssertion(() => Assert.Contains("Sri Sundha Mobiles", page.Find(".receipt-match").TextContent, StringComparison.Ordinal));
        Assert.Empty(page.FindAll(".receipt-no-match"));
        Assert.DoesNotContain("not this one", _ele.Current?.Line ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void A_receipt_shared_again_has_ele_say_where_it_already_is()
    {
        _receipts.Rows.Add(new EleFi.Domain.Receipts.Receipt { TransactionId = _shop.Id, Reference = "432167890123", Payee = "Sri Sundha Mobiles" });
        var page = Render(GPayScreen, [_shop]);

        page.WaitForAssertion(() => Assert.Equal("You attached this receipt to Sri Sundha Mobiles already.", _ele.Current?.Line));
        Assert.Equal("Open it", Assert.Single(_ele.Current!.Choices).Label);
    }

    [Fact]
    public void A_shop_receipts_rows_can_become_the_transactions_items_after_unticking_what_is_not_one()
    {
        var gnani = Paid("Gnani Mart", 455_00);
        var page = Render(ShopReceipt, [gnani]);
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".read-item").Count));

        page.FindAll(".read-item-on")[1].Change(false);
        page.FindAll("button").First(b => b.TextContent.Trim() == "Attach and use these items").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Equal(["Rice 5kg", null], gnani.Items.Select(i => i.Name));
            Assert.Equal([450_00, 5_00], gnani.Items.Select(i => i.AmountMinor));
        });
    }

    [Fact]
    public void A_receipt_that_matches_nothing_is_offered_as_a_new_transaction_for_review()
    {
        var chai = new Party { Kind = PartyKind.External, Name = "Chai Point" };
        _parties.GetOrCreateExternalAsync("Chai Point", Arg.Any<CancellationToken>()).Returns(chai);
        _parties.FindAsync(chai.Id, Arg.Any<CancellationToken>()).Returns(chai);
        _ledger.When(l => l.AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>())).Do(call =>
        {
            var added = call.Arg<Transaction>();
            added.SourceParty = _bankParty;
            added.DestinationParty = chai;
            _ledger.FindAsync(added.Id, Arg.Any<CancellationToken>()).Returns(added);
        });

        var page = Render(new ScannedReceipt(["Chai Point", "Masala chai 40.00", "Total 40.00"], null, null), []);
        page.WaitForAssertion(() => Assert.Contains("Nothing within three days matches", page.Markup, StringComparison.Ordinal));

        page.Find("button.record-new").Click();

        page.WaitForAssertion(() =>
        {
            var receipt = Assert.Single(_receipts.Rows);
            _ledger.Received().AddAsync(Arg.Is<Transaction>(t => t.NeedsReview && t.SourceAmountMinor == 40_00 && t.CaptureSource == CaptureSource.ShareSheet), Arg.Any<CancellationToken>());
            Assert.NotEqual(Guid.Empty, receipt.TransactionId);
        });
    }

    [Fact]
    public void Text_with_no_amount_says_so_rather_than_guessing()
    {
        var page = Render(new ScannedReceipt(["A photo of a cat"], null, null), []);

        page.WaitForAssertion(() => Assert.Contains("could not find an amount", page.Markup, StringComparison.Ordinal));

        // Comforting Ele, with the way on right there: another photo, without leaving.
        Assert.NotNull(page.Find(".ele-centre .retry-gallery"));
        Assert.NotNull(page.Find(".ele-centre .retry-camera"));
    }

    private IRenderedComponent<ReceiptReview> Render(ScannedReceipt scan, IReadOnlyList<Transaction> nearby) =>
        Render(new Fakes.FixedScanner(scan), nearby, "receipt");

    private IRenderedComponent<ReceiptReview> Render(Fakes.FixedScanner scanner, IReadOnlyList<Transaction> nearby, string route)
    {
        var clock = new Fakes.StoppedClock();
        var labels = new Fakes.SeededLabels();
        var apps = Substitute.For<IAppRepository>();

        _ledger.QueryAsync(Arg.Any<TransactionFilter>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(nearby);
        foreach (var transaction in nearby)
        {
            _ledger.FindAsync(transaction.Id, Arg.Any<CancellationToken>()).Returns(transaction);
        }

        _containers.ListSelectableAsync(Arg.Any<CancellationToken>()).Returns([_bank]);
        _containers.PartyForAsync(_bank.Id, Arg.Any<CancellationToken>()).Returns(_bankParty);
        _containers.FindByLast4Async(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _parties.FindAsync(_bankParty.Id, Arg.Any<CancellationToken>()).Returns(_bankParty);
        foreach (var party in nearby.Select(t => t.DestinationParty!))
        {
            _parties.FindAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);
        }

        var contacts = Substitute.For<IContactRepository>();
        contacts.AliasesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        var editing = new EditTransactionService(_ledger, _parties, labels, Substitute.For<IAuditRepository>(), clock, apps);
        Services.AddSingleton<IReceiptScanner>(scanner);
        Services.AddSingleton(new ReceiptMatcher(_ledger, clock));
        Services.AddSingleton(new ReceiptService(_receipts, _ledger, _containers, _parties, contacts, new CaptureService(_ledger, _parties, apps, labels, clock), editing, clock));
        Services.AddSingleton(editing);
        Services.AddSingleton(_ele);
        Services.AddEleAndUndo();

        Services.GetRequiredService<NavigationManager>().NavigateTo(route);
        return RenderComponent<ReceiptReview>();
    }

    private Transaction Paid(string payee, long minor)
    {
        var to = new Party { Kind = PartyKind.External, Name = payee };
        return new Transaction
        {
            SourcePartyId = _bankParty.Id,
            SourceParty = _bankParty,
            DestinationPartyId = to.Id,
            DestinationParty = to,
            SourceAmountMinor = minor,
            DestinationAmountMinor = minor,
            OccurredOn = new DateOnly(2026, 8, 30),
            OccurredAtTime = new TimeOnly(19, 12),
        };
    }
}
