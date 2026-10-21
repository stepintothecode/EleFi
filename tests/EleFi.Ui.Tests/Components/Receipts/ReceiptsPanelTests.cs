using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Receipts;
using EleFi.Application.Transactions;
using EleFi.Domain.Parties;
using EleFi.Domain.Receipts;
using EleFi.Domain.Transactions;
using EleFi.Ui.Components.Receipts;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Receipts;

/// <summary>A transaction's receipts on its edit screen: whether each agrees, and what can be done.</summary>
public class ReceiptsPanelTests : Bunit.TestContext
{
    private readonly ITransactionRepository _ledger = Substitute.For<ITransactionRepository>();
    private readonly IPartyRepository _parties = Substitute.For<IPartyRepository>();
    private readonly Fakes.MemoryReceipts _receipts = new();
    private readonly Transaction _shop;

    public ReceiptsPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var bank = new Party { Kind = PartyKind.Container };
        var gnani = new Party { Kind = PartyKind.External, Name = "Gnani Mart" };
        _shop = new Transaction
        {
            SourcePartyId = bank.Id,
            SourceParty = bank,
            DestinationPartyId = gnani.Id,
            DestinationParty = gnani,
            SourceAmountMinor = 500_00,
            DestinationAmountMinor = 500_00,
            OccurredOn = new DateOnly(2026, 8, 30),
        };
        _ledger.FindAsync(_shop.Id, Arg.Any<CancellationToken>()).Returns(_shop);
        _parties.FindAsync(bank.Id, Arg.Any<CancellationToken>()).Returns(bank);
        _parties.FindAsync(gnani.Id, Arg.Any<CancellationToken>()).Returns(gnani);

        var clock = new Fakes.StoppedClock();
        var labels = new Fakes.SeededLabels();
        var apps = Substitute.For<IAppRepository>();
        var contacts = Substitute.For<IContactRepository>();
        contacts.AliasesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var editing = new EditTransactionService(_ledger, _parties, labels, Substitute.For<IAuditRepository>(), clock, apps);
        Services.AddSingleton(new ReceiptService(_receipts, _ledger, new Fakes.EmptyContainers(), _parties, contacts, new CaptureService(_ledger, _parties, apps, labels, clock), editing, clock));
        Services.AddEleAndUndo();
    }

    [Fact]
    public void Only_a_receipt_that_disagrees_is_flagged()
    {
        Attach(500_00);
        Attach(520_00);

        var panel = Render();

        var rows = panel.FindAll(".receipt-row");
        Assert.Empty(rows[0].QuerySelectorAll(".pill"));
        Assert.Equal("Amount differs", rows[1].QuerySelector(".pill")!.TextContent);
        Assert.Contains("Shop receipt · Gnani Mart", panel.Find(".receipt-title").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_receipt_that_disagrees_offers_its_amount_or_keeping_the_transaction_as_it_is()
    {
        Attach(520_00);
        var panel = Render();

        panel.Find(".receipt-row").Click();

        Assert.Contains("₹520.00 on the receipt, ₹500.00 here", panel.Find(".receipt-warning").TextContent, StringComparison.Ordinal);
        Assert.Contains("Use ₹520.00 from the receipt", panel.Find(".use-amount").TextContent, StringComparison.Ordinal);

        panel.Find(".keep-it").Click();

        panel.WaitForAssertion(() => Assert.Equal("Kept as is", panel.Find(".receipt-row .pill").TextContent));
        Assert.NotNull(_receipts.Rows[0].SettledAt);
    }

    [Fact]
    public void Removing_a_receipt_hides_it_and_can_be_undone()
    {
        Attach(500_00);
        var panel = Render();

        panel.Find(".receipt-row").Click();
        panel.Find(".remove-receipt").Click();

        panel.WaitForAssertion(() => Assert.Empty(panel.FindAll(".receipt-row")));
        Assert.True(Services.GetRequiredService<UndoHistory>().CanUndo(UndoHistory.Transactions));
    }

    [Fact]
    public void Attach_a_receipt_offers_the_camera_or_a_photo_and_opens_the_receipt_screen()
    {
        var panel = Render();

        panel.Find(".items-add").Click();
        panel.Find(".attach-gallery").Click();

        Assert.EndsWith($"receipt?for={_shop.Id}&from=gallery", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    private void Attach(long minor) => _receipts.Rows.Add(new Receipt
    {
        TransactionId = _shop.Id,
        Kind = ReceiptKind.Shop,
        Payee = "Gnani Mart",
        AmountMinor = minor,
        CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(_receipts.Rows.Count),
    });

    private IRenderedComponent<ReceiptsPanel> Render() =>
        RenderComponent<ReceiptsPanel>(p => p.Add(c => c.TransactionId, _shop.Id).Add(c => c.Transaction, _shop));
}
