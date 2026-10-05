using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Transactions;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>Deleted transactions, and getting them back.</summary>
public class DeletedTransactionsTests : Bunit.TestContext
{
    private readonly ITransactionRepository _ledger = Substitute.For<ITransactionRepository>();
    private readonly List<Transaction> _deleted = [];

    public DeletedTransactionsTests()
    {
        _ledger.ListDeletedAsync(Arg.Any<CancellationToken>()).Returns(_ => _deleted.ToList());
        _ledger.When(l => l.RestoreAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(call => _deleted.RemoveAll(t => t.Id == call.Arg<Guid>()));

        var clock = new Fakes.StoppedClock();
        Services.AddSingleton(new EditTransactionService(
            _ledger, Substitute.For<IPartyRepository>(), new Fakes.SeededLabels(), Substitute.For<IAuditRepository>(), clock));
        Services.AddSingleton(new ToastService());
    }

    [Fact]
    public void Each_deleted_transaction_has_a_restore_button_and_restoring_removes_it_from_here()
    {
        _deleted.Add(Spend("Zomato"));
        var page = RenderComponent<DeletedTransactions>();

        page.Find("button[aria-label='Restore Zomato']").Click();

        page.WaitForAssertion(() => Assert.Contains("Nothing deleted", page.Markup, StringComparison.Ordinal));
        _ledger.Received().RestoreAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Restore_all_brings_every_one_back()
    {
        _deleted.Add(Spend("Zomato"));
        _deleted.Add(Spend("Swiggy"));
        var page = RenderComponent<DeletedTransactions>();

        page.Find(".section-head button").Click();

        page.WaitForAssertion(() => Assert.Empty(_deleted));
    }

    [Fact]
    public void With_nothing_deleted_it_says_so_and_offers_no_restore_all()
    {
        var page = RenderComponent<DeletedTransactions>();

        Assert.Contains("Nothing deleted", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".section-head button"));
    }

    private static Transaction Spend(string payee)
    {
        var card = new Party { Kind = PartyKind.Container, ContainerId = Guid.NewGuid() };
        var shop = new Party { Kind = PartyKind.External, Name = payee };

        return new Transaction
        {
            SourcePartyId = card.Id,
            SourceParty = card,
            DestinationPartyId = shop.Id,
            DestinationParty = shop,
            SourceAmountMinor = 100,
            DestinationAmountMinor = 100,
            DeletedAt = DateTimeOffset.UnixEpoch,
        };
    }
}
