using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Export;
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

/// <summary>The list: filters in a sheet, a total at the end, and coming back to where you were.</summary>
public class TransactionsTests : Bunit.TestContext
{
    private readonly ITransactionRepository _ledger = Substitute.For<ITransactionRepository>();
    private readonly TransactionListState _state = new();
    private readonly Transaction _lunch;

    public TransactionsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var card = new Party { Kind = PartyKind.Container, ContainerId = Guid.NewGuid() };
        var zomato = new Party { Kind = PartyKind.External, Name = "Zomato" };
        _lunch = new Transaction
        {
            SourcePartyId = card.Id,
            SourceParty = card,
            DestinationPartyId = zomato.Id,
            DestinationParty = zomato,
            SourceAmountMinor = 45000,
            DestinationAmountMinor = 45000,
            OccurredOn = new DateOnly(2026, 8, 30),
        };

        _ledger.QueryAsync(Arg.Any<TransactionFilter>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<int>(1) == 0 ? [_lunch] : Array.Empty<Transaction>());
        _ledger.CountAsync(Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>()).Returns(1);
        _ledger.TotalsAsync(Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>()).Returns(new FlowTotals(100000, 45000));

        var clock = new Fakes.StoppedClock();
        var containers = Substitute.For<IContainerRepository>();
        containers.ListSelectableAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Container { Name = "SBI", Kind = ContainerKind.BankAccount },
            new Container { Name = "Amex", Kind = ContainerKind.CreditCard },
        ]);

        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton(_ledger);
        Services.AddSingleton(containers);
        Services.AddSingleton<ILabelRepository>(new Fakes.SeededLabels());
        Services.AddSingleton(new CsvExporter(_ledger, clock));
        Services.AddSingleton(Substitute.For<IFileShare>());
        Services.AddSingleton(new ToastService());
        Services.AddSingleton(_state);
    }

    [Fact]
    public void Filters_open_in_a_sheet_rather_than_in_the_page()
    {
        var page = RenderComponent<Transactions>();
        Assert.Empty(page.FindAll("[role=dialog]"));

        page.Find(".section-head button").Click();

        Assert.Equal("Filters", page.Find("[role=dialog]").GetAttribute("aria-label"));
    }

    [Fact]
    public void Today_and_this_week_are_offered_and_containers_are_in_picker_order()
    {
        var page = RenderComponent<Transactions>();
        page.Find(".section-head button").Click();

        var chips = page.FindAll("[role=dialog] .chip").Select(c => c.TextContent.Trim()).ToList();

        Assert.Contains("Today", chips);
        Assert.Contains("This week", chips);
        Assert.Contains("Custom range", chips);
        Assert.True(chips.IndexOf("Amex") < chips.IndexOf("SBI"), "Cards come before banks, as in every picker.");
        Assert.NotEmpty(page.FindAll("[role=dialog] hr.divider"));
    }

    [Fact]
    public void Choosing_today_filters_to_today()
    {
        var page = RenderComponent<Transactions>();
        page.Find(".section-head button").Click();

        page.FindAll("[role=dialog] .chip").First(c => c.TextContent.Trim() == "Today").Click();

        Assert.Equal(new DateOnly(2026, 8, 30), _state.Filter.From);
        Assert.Equal(new DateOnly(2026, 8, 30), _state.Filter.To);
    }

    [Fact]
    public void The_list_ends_with_its_total_in_out_and_net()
    {
        var page = RenderComponent<Transactions>();

        var total = page.Find(".list-total");
        Assert.Contains("+₹550.00", total.TextContent, StringComparison.Ordinal);
        Assert.Contains("₹1,000.00", total.TextContent, StringComparison.Ordinal);
        Assert.Contains("₹450.00", total.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Opening_a_row_and_coming_back_keeps_the_filter_and_marks_the_row()
    {
        var first = RenderComponent<Transactions>();
        first.Find(".section-head button").Click();
        first.FindAll("[role=dialog] .chip").First(c => c.TextContent.Trim() == "Needs review").Click();
        first.Find("a.txn-link").Click();

        // The editor opens and the list is torn down; this is the way back.
        first.Dispose();
        var again = RenderComponent<Transactions>();

        Assert.True(_state.Filter.NeedsReview);
        Assert.Contains("returned", again.Find("a.txn-link").ClassName, StringComparison.Ordinal);
    }
}
