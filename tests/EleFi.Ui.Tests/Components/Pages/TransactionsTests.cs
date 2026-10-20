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
        Services.AddEleAndUndo();
        Services.AddSingleton(_state);
    }

    [Fact]
    public void Filters_open_in_a_sheet_rather_than_in_the_page()
    {
        var page = RenderComponent<Transactions>();
        Assert.Empty(page.FindAll("[role=dialog]"));

        page.Find(".section-head button.open-filters").Click();

        Assert.Equal("Filters", page.Find("[role=dialog]").GetAttribute("aria-label"));
    }

    [Fact]
    public void Each_filter_is_one_compact_row_with_nothing_listed_until_it_is_opened()
    {
        var page = RenderComponent<Transactions>();
        page.Find(".section-head button.open-filters").Click();

        var rows = page.FindAll("[role=dialog] .filter-row .filter-label").Select(l => l.TextContent.Trim());
        Assert.Equal(["Dates", "Kind", "Container", "Label", "Needs review only"], rows);
        Assert.Empty(page.FindAll("[role=dialog] [role=option]"));
        Assert.All(page.FindAll("[role=dialog] .filter-value"), v => Assert.Equal("Any", v.TextContent.Trim()));
    }

    [Fact]
    public void Today_and_this_week_are_offered_and_containers_are_in_picker_order()
    {
        var page = RenderComponent<Transactions>();
        page.Find(".section-head button.open-filters").Click();

        page.Find("#filter-dates").Click();
        var dates = Options(page);
        Assert.Contains("Today", dates);
        Assert.Contains("This week", dates);
        Assert.Contains("Custom range", dates);

        // One open at a time: opening Container closes Dates.
        page.Find("#filter-container").Click();
        var containers = Options(page);
        Assert.DoesNotContain("Today", containers);
        Assert.True(containers.IndexOf("Amex") < containers.IndexOf("SBI"), "Cards come before banks, as in every picker.");
    }

    [Fact]
    public void Choosing_today_filters_to_today_and_says_so_on_the_row()
    {
        var page = RenderComponent<Transactions>();
        page.Find(".section-head button.open-filters").Click();

        page.Find("#filter-dates").Click();
        page.FindAll("[role=dialog] [role=option]").First(c => c.TextContent.Trim() == "Today").Click();

        Assert.Equal(new DateOnly(2026, 8, 30), _state.Filter.From);
        Assert.Equal(new DateOnly(2026, 8, 30), _state.Filter.To);
        Assert.Equal("Today", page.Find("#filter-dates .filter-value").TextContent.Trim());
        Assert.Empty(page.FindAll("[role=dialog] [role=option]"));
    }

    [Fact]
    public void Clear_filters_is_in_the_head_of_the_sheet_and_clears_everything()
    {
        var page = RenderComponent<Transactions>();
        page.Find(".section-head button.open-filters").Click();
        Assert.True(page.Find(".modal-head .clear-filters").HasAttribute("disabled"));

        page.Find("#filter-kind").Click();
        page.FindAll("[role=dialog] [role=option]").First(c => c.TextContent.Trim() == "Debit").Click();
        Assert.NotEmpty(_state.Filter.Kinds);

        page.Find(".modal-head .clear-filters").Click();

        Assert.True(_state.Filter.IsEmpty);
    }

    private static List<string> Options(IRenderedComponent<Transactions> page) =>
        page.FindAll("[role=dialog] [role=option]").Select(o => o.TextContent.Trim()).ToList();

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
    public void Opened_from_a_contact_the_filters_say_whose_transactions_these_are_and_one_tap_clears_it()
    {
        _state.ShowContact(new TransactionFilter { PartyIds = [Guid.NewGuid()] }, "Rahul and 1 linked");
        var page = RenderComponent<Transactions>();
        page.Find(".section-head button.open-filters").Click();

        Assert.Equal("Rahul and 1 linked", page.Find("[role=dialog] .filter-contact .filter-value").TextContent.Trim());
        page.Find("[role=dialog] .filter-contact .filter-remove").Click();

        Assert.Empty(_state.Filter.PartyIds);
    }

    [Fact]
    public void In_and_out_each_get_a_line_of_their_own()
    {
        var page = RenderComponent<Transactions>();

        var lines = page.FindAll(".list-total .flow-line");

        Assert.Equal(2, lines.Count);
        Assert.StartsWith("In", lines[0].TextContent.Trim(), StringComparison.Ordinal);
        Assert.Contains("₹1,000.00", lines[0].TextContent, StringComparison.Ordinal);
        Assert.StartsWith("Out", lines[1].TextContent.Trim(), StringComparison.Ordinal);
        Assert.Contains("₹450.00", lines[1].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Opening_a_row_and_coming_back_keeps_the_filter_and_marks_the_row()
    {
        var first = RenderComponent<Transactions>();
        first.Find(".section-head button.open-filters").Click();
        first.Find("[role=dialog] input[aria-label='Needs review only']").Change(true);
        first.Find("a.txn-link").Click();

        // The editor opens and the list is torn down; this is the way back.
        first.Dispose();
        var again = RenderComponent<Transactions>();

        Assert.True(_state.Filter.NeedsReview);
        Assert.Contains("returned", again.Find("a.txn-link").ClassName, StringComparison.Ordinal);
    }
}
