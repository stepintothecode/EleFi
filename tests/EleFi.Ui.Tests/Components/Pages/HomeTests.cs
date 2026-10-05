using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Containers;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using EleFi.Domain.Balances;
using EleFi.Domain.Containers;
using EleFi.Domain.Money;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>
/// The dashboard on a phone that has just installed the app.
/// </summary>
/// <remarks>
/// This is the regression test for the first-launch crash. The net-worth tiles rendered on
/// the synchronous first pass, before <c>OnInitializedAsync</c> had run, and formatted a
/// <c>default(NetWorth)</c> whose currency had a null code. That threw
/// <c>ArgumentNullException</c> out of a dictionary lookup, Blazor caught it at the top of
/// the tree, and the whole app showed "An unhandled error has occurred" on a blank screen.
/// </remarks>
public class HomeTests : Bunit.TestContext
{
    [Fact]
    public void It_renders_on_a_completely_empty_database()
    {
        Register();

        var component = RenderComponent<Home>();

        // Rendering at all is the assertion. Before the fix this threw during the first
        // synchronous pass, so there was nothing to assert against.
        Assert.Contains("Net worth", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Liquid", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void It_shows_the_empty_state_that_points_at_the_next_action()
    {
        Register();

        var component = RenderComponent<Home>();

        // An empty state that explains nothing is a dead end on the one screen a new user
        // sees first.
        Assert.Contains("Add a container", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlapping_labels_are_explained_where_the_bars_are()
    {
        // 2,000 spent once, labelled both Food and Household. The bars add up to 4,000.
        Register(new SpendBreakdown(
            [
                new LabelSpend(Guid.NewGuid(), "Food", "#f97316", 2_000_00),
                new LabelSpend(Guid.NewGuid(), "Household", "#0ea5e9", 2_000_00),
            ],
            2_000_00));

        var component = RenderComponent<Home>();

        // Someone who adds the bars up and gets more than they spent is told why on the same
        // screen, rather than concluding the app cannot count.
        Assert.Contains("counted under each", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Labels_that_do_not_overlap_get_no_explanation()
    {
        // Two labels, but on different transactions, so the bars sum to the total exactly.
        Register(new SpendBreakdown(
            [
                new LabelSpend(Guid.NewGuid(), "Food", "#f97316", 1_200_00),
                new LabelSpend(Guid.NewGuid(), "Travel", "#0ea5e9", 800_00),
            ],
            2_000_00));

        var component = RenderComponent<Home>();

        // Explaining a discrepancy that is not on screen teaches the reader to distrust a
        // total that was right all along.
        Assert.DoesNotContain("counted under each", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Transactions_needing_review_are_announced_and_link_to_just_those()
    {
        Register(needsReview: 2);

        var component = RenderComponent<Home>();

        var banner = component.Find("a.inbox-banner");
        Assert.Equal("transactions", banner.GetAttribute("href"));
        Assert.Contains("2 transactions need review", banner.TextContent, StringComparison.Ordinal);

        banner.Click();
        Assert.True(Services.GetRequiredService<EleFi.Ui.Services.TransactionListState>().Filter.NeedsReview);
    }

    [Fact]
    public void With_nothing_to_review_there_is_no_banner()
    {
        Register();

        var component = RenderComponent<Home>();

        Assert.Empty(component.FindAll("a.inbox-banner"));
    }

    [Fact]
    public void This_month_is_the_tab_shown_first()
    {
        Register();

        var component = RenderComponent<Home>();

        Assert.Equal("This month", component.Find(".segmented [aria-selected=true]").TextContent);
        Assert.Equal(new DateOnly(2026, 8, 1), _transactions.LastSpendFilter!.From);
        Assert.Equal(new DateOnly(2026, 8, 30), _transactions.LastSpendFilter.To);
    }

    [Fact]
    public void Last_month_is_one_tap_away()
    {
        Register();
        var component = RenderComponent<Home>();

        component.FindAll(".segmented button")[1].Click();

        Assert.Equal("Last month", component.Find(".segmented [aria-selected=true]").TextContent);
        Assert.Equal(new DateOnly(2026, 7, 1), _transactions.LastSpendFilter!.From);
        Assert.Equal(new DateOnly(2026, 7, 31), _transactions.LastSpendFilter.To);
    }

    [Fact]
    public void Select_a_range_asks_for_dates_and_times_and_then_shows_that_range()
    {
        Register();
        var component = RenderComponent<Home>();

        component.FindAll(".segmented button")[2].Click();
        Assert.Equal("Select a range", component.Find("[role=dialog]").GetAttribute("aria-label"));

        component.Find("#range-from").Change("2026-08-10");
        component.Find("#range-from-time").Change("18:00:00");
        component.Find("#range-to").Change("2026-08-12");
        component.Find("[role=dialog] button.primary").Click();

        Assert.Equal(new DateOnly(2026, 8, 10), _transactions.LastSpendFilter!.From);
        Assert.Equal(new TimeOnly(18, 0), _transactions.LastSpendFilter.FromTime);
        Assert.Equal(new DateOnly(2026, 8, 12), _transactions.LastSpendFilter.To);
        Assert.Contains("10 Aug 18:00 to 12 Aug", component.Find(".segmented [aria-selected=true]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Tapping_ele_hides_every_amount_and_ele_covers_its_eyes()
    {
        Register();
        var component = RenderComponent<Home>();

        component.Find(".mascot-toggle").Click();

        Assert.True(_privacy.AmountsHidden);
        Assert.Equal("Mascot, covering its eyes", component.Find("canvas").GetAttribute("aria-label"));
        Assert.Equal("Show amounts", component.Find(".mascot-toggle").GetAttribute("aria-label"));
    }

    [Fact]
    public void A_containers_bank_and_last_four_sit_under_its_name()
    {
        Register(balances: [new ContainerBalance(Guid.NewGuid(), "Savings", ContainerKind.BankAccount, Money.FromMinor(100, Currency.Inr), "HDFC Bank ••4417")]);

        var component = RenderComponent<Home>();

        Assert.Equal("HDFC Bank ••4417", component.Find(".txn .detail").TextContent);
    }

    private readonly EleFi.Ui.Services.PrivacyMode _privacy = new(new Fakes.MemorySettings());
    private readonly INoticeRepository _notices = NSubstitute.Substitute.For<INoticeRepository>();

    [Fact]
    public void The_bell_shows_how_many_notifications_are_unread_and_opens_the_list()
    {
        NSubstitute.SubstituteExtensions.Returns(_notices.UnreadCountAsync(NSubstitute.Arg.Any<CancellationToken>()), 3);
        Register();

        var component = RenderComponent<Home>();

        var bell = component.Find("a.bell");
        Assert.Equal("notifications", bell.GetAttribute("href"));
        Assert.Equal("3", component.Find(".bell-count").TextContent);
    }

    [Fact]
    public void The_streak_chip_opens_a_sheet_explaining_it()
    {
        Register();
        var component = RenderComponent<Home>();

        component.Find(".streak-chip").Click();

        Assert.Equal("Steady ledger", component.Find("[role=dialog]").GetAttribute("aria-label"));
        Assert.Contains("Sunday night", component.Find("[role=dialog]").TextContent, StringComparison.Ordinal);
    }
    private Fakes.EmptyTransactions _transactions = new();

    private void Register(SpendBreakdown? spend = null, int needsReview = 0, IReadOnlyList<ContainerBalance>? balances = null)
    {
        var clock = new Fakes.StoppedClock();
        var containers = new Fakes.EmptyContainers();
        _transactions = new Fakes.EmptyTransactions
        {
            Spend = spend ?? new SpendBreakdown([], 0),
            NeedsReviewCount = needsReview,
            Balances = balances ?? [],
        };

        Services.AddSingleton(_privacy);
        Services.AddSingleton(_notices);
        Services.AddSingleton(new EleFi.Ui.Services.TransactionListState());
        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton<ITransactionRepository>(_transactions);
        Services.AddSingleton<IContainerRepository>(containers);
        Services.AddSingleton(new ContainerService(containers, _transactions, clock));
        Services.AddSingleton<IMascotService>(new Fakes.SilentMascot());
    }
}