using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Containers;
using EleFi.Domain.Balances;
using EleFi.Domain.Containers;
using EleFi.Domain.Money;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

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
    public void Today_is_the_first_tab_and_shows_just_today()
    {
        Register();
        var component = RenderComponent<Home>();

        var tabs = component.FindAll(".segmented button").Select(b => b.TextContent).ToList();
        Assert.Equal(["Today", "This month", "Last month", "Range"], tabs);

        component.FindAll(".segmented button")[0].Click();

        Assert.Equal("Today", component.Find(".segmented [aria-selected=true]").TextContent);
        Assert.Equal(new DateOnly(2026, 8, 30), _transactions.LastSpendFilter!.From);
        Assert.Equal(new DateOnly(2026, 8, 30), _transactions.LastSpendFilter.To);
    }

    [Fact]
    public void Last_month_is_one_tap_away()
    {
        Register();
        var component = RenderComponent<Home>();

        component.FindAll(".segmented button").First(b => b.TextContent == "Last month").Click();

        Assert.Equal("Last month", component.Find(".segmented [aria-selected=true]").TextContent);
        Assert.Equal(new DateOnly(2026, 7, 1), _transactions.LastSpendFilter!.From);
        Assert.Equal(new DateOnly(2026, 7, 31), _transactions.LastSpendFilter.To);
    }

    [Fact]
    public void Range_asks_for_dates_and_times_and_then_shows_that_range_briefly()
    {
        Register();
        var component = RenderComponent<Home>();

        component.FindAll(".segmented button").First(b => b.TextContent == "Range").Click();
        Assert.Equal("Select a range", component.Find("[role=dialog]").GetAttribute("aria-label"));

        component.Find("#range-from").Change("2026-08-10");
        component.Find("#range-from-time").Change("18:00:00");
        component.Find("#range-to").Change("2026-08-12");
        component.Find("[role=dialog] button.primary").Click();

        Assert.Equal(new DateOnly(2026, 8, 10), _transactions.LastSpendFilter!.From);
        Assert.Equal(new TimeOnly(18, 0), _transactions.LastSpendFilter.FromTime);
        Assert.Equal(new DateOnly(2026, 8, 12), _transactions.LastSpendFilter.To);
        // Short enough for a quarter of the bar; the 18:00 still applies to the figures.
        Assert.Equal("10-12 Aug", component.Find(".segmented [aria-selected=true]").TextContent);
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
    public void A_new_user_with_nothing_set_up_gets_eles_guide_once()
    {
        var guide = new EleFi.Ui.Services.EleGuide(new Fakes.MemorySettings());
        Services.AddSingleton(guide);
        Register();

        RenderComponent<Home>();

        Assert.True(guide.IsOpen);
    }

    [Fact]
    public void Someone_with_containers_is_not_interrupted_by_the_guide()
    {
        var guide = new EleFi.Ui.Services.EleGuide(new Fakes.MemorySettings());
        Services.AddSingleton(guide);
        Register(balances: [new ContainerBalance(Guid.NewGuid(), "Savings", ContainerKind.BankAccount, Money.FromMinor(100, Currency.Inr))]);

        RenderComponent<Home>();

        Assert.False(guide.IsOpen);
    }

    [Fact]
    public void Coming_back_to_the_app_shows_what_was_recorded_meanwhile()
    {
        Register(needsReview: 0);
        var component = RenderComponent<Home>();
        Assert.Empty(component.FindAll("a.inbox-banner"));

        // An SMS recorded a payment while EleFi was in the background.
        _transactions.NeedsReviewCount = 1;
        ((Fakes.ManualResume)Services.GetRequiredService<IAppResume>()).Resume();

        component.WaitForAssertion(() => Assert.NotNull(component.Find("a.inbox-banner")));
    }

    [Fact]
    public void Containers_are_not_listed_on_home_any_more()
    {
        Register(balances: [new ContainerBalance(Guid.NewGuid(), "Savings", ContainerKind.BankAccount, Money.FromMinor(100, Currency.Inr))]);

        var component = RenderComponent<Home>();

        // The Containers tab shows the list; Home shows the figures it adds up to.
        Assert.DoesNotContain("<h2>Containers</h2>", component.Markup, StringComparison.Ordinal);
        Assert.Empty(component.FindAll(".txn"));
    }

    [Fact]
    public void Tapping_liquid_lists_the_containers_that_make_it_up_and_nothing_else()
    {
        Register(balances:
        [
            new ContainerBalance(Guid.NewGuid(), "Savings", ContainerKind.BankAccount, Money.FromMinor(100_00, Currency.Inr), "HDFC Bank ••4417"),
            new ContainerBalance(Guid.NewGuid(), "FD", ContainerKind.FixedDeposit, Money.FromMinor(5_000_00, Currency.Inr)),
            new ContainerBalance(Guid.NewGuid(), "Card", ContainerKind.CreditCard, Money.SignedMinor(-300_00, Currency.Inr)),
        ]);
        var component = RenderComponent<Home>();

        component.Find("button.tile[aria-label^='Liquid']").Click();

        var sheet = component.Find("[role=dialog]");
        Assert.Equal(["Savings"], sheet.QuerySelectorAll(".who").Select(w => w.TextContent));
        Assert.Equal("HDFC Bank ••4417", sheet.QuerySelector(".detail")!.TextContent);
    }

    [Fact]
    public void Tapping_owed_lists_the_cards()
    {
        Register(balances:
        [
            new ContainerBalance(Guid.NewGuid(), "Savings", ContainerKind.BankAccount, Money.FromMinor(100_00, Currency.Inr)),
            new ContainerBalance(Guid.NewGuid(), "Card", ContainerKind.CreditCard, Money.SignedMinor(-300_00, Currency.Inr)),
        ]);
        var component = RenderComponent<Home>();

        component.Find("button.tile[aria-label^='Owed']").Click();

        Assert.Equal(["Card"], component.Find("[role=dialog]").QuerySelectorAll(".who").Select(w => w.TextContent));
    }

    [Fact]
    public void A_card_with_nothing_owed_is_not_shown_in_the_owed_colour()
    {
        Register(balances:
        [
            new ContainerBalance(Guid.NewGuid(), "Paid off", ContainerKind.CreditCard, Money.SignedMinor(0, Currency.Inr)),
            new ContainerBalance(Guid.NewGuid(), "In use", ContainerKind.CreditCard, Money.SignedMinor(-300_00, Currency.Inr)),
        ]);
        var component = RenderComponent<Home>();

        component.Find("button.tile[aria-label^='Owed']").Click();

        var amounts = component.FindAll("[role=dialog] .txn .amount");
        Assert.False(amounts[0].ClassList.Contains("owed"));
        Assert.True(amounts[1].ClassList.Contains("owed"));
    }

    [Fact]
    public void Still_planned_this_month_shows_what_is_left_to_pay()
    {
        Register(open:
        [
            new EleFi.Domain.Planning.Plan { Title = "Rent", DueOn = new DateOnly(2026, 8, 31), AmountMinor = 25_000_00, Direction = EleFi.Domain.Transactions.TransactionKind.Debit },
        ]);

        var component = RenderComponent<Home>();

        var card = component.Find("a.still-planned");
        Assert.Contains("₹25,000.00", card.TextContent, StringComparison.Ordinal);
        Assert.Contains("1 plan", card.TextContent, StringComparison.Ordinal);
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
        Assert.Contains("by Sunday", component.Find("[role=dialog]").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("not being counted", component.Find("[role=dialog]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_streak_sheet_says_the_rule_in_one_line_and_little_else()
    {
        Register(startedAt: new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero));
        var component = RenderComponent<Home>();

        component.Find(".streak-chip").Click();

        var words = component.Find("[role=dialog]").TextContent
            .Split((char[])[' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.InRange(words, 1, 75);
        Assert.Equal(2, component.Find(".streak-rule").TextContent.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Count(s => s.Trim().Length > 0));
    }

    [Fact]
    public void The_streak_is_counted_and_says_what_this_week_still_needs()
    {
        // Started three weeks ago with nothing to review: two clean weeks and a tidy one so far.
        Register(startedAt: new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero), reviewOn: [new DateOnly(2026, 8, 28)]);
        var component = RenderComponent<Home>();

        // 30 Aug is a Sunday: the weeks of 10 and 17 Aug were clean, and this one has a review left.
        Assert.Contains("2 weeks", component.Find(".streak-chip").TextContent, StringComparison.Ordinal);

        component.Find(".streak-chip").Click();
        Assert.Contains("review 1 transaction", component.Find(".streak-now").TextContent, StringComparison.Ordinal);
    }
    private Fakes.EmptyTransactions _transactions = new();

    private void Register(
        SpendBreakdown? spend = null,
        int needsReview = 0,
        IReadOnlyList<ContainerBalance>? balances = null,
        IReadOnlyList<EleFi.Domain.Planning.Plan>? open = null,
        DateTimeOffset? startedAt = null,
        IReadOnlyList<DateOnly>? reviewOn = null)
    {
        var clock = new Fakes.StoppedClock();
        var containers = new Fakes.EmptyContainers();
        _transactions = new Fakes.EmptyTransactions
        {
            Spend = spend ?? new SpendBreakdown([], 0),
            NeedsReviewCount = needsReview,
            Balances = balances ?? [],
            Facts = new ReviewFacts(startedAt, reviewOn ?? []),
        };

        var plans = Substitute.For<IPlanRepository>();
        plans.ListOpenAsync(Arg.Any<CancellationToken>()).Returns(open ?? []);
        var goals = Substitute.For<IGoalRepository>();
        goals.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<EleFi.Domain.Goals.Goal>());

        Services.AddSingleton<ISettingsStore>(new Fakes.MemorySettings());
        Services.AddEleAndUndo();
        Services.AddSingleton(new EleFi.Application.Streaks.SteadyLedgerService(_transactions, plans, clock));
        Services.AddSingleton(new EleFi.Application.Planning.PlanService(plans, _transactions, clock, Substitute.For<IAuditRepository>(), Substitute.For<IReminderSchedule>()));
        Services.AddSingleton(new EleFi.Application.Goals.GoalService(goals, _transactions, clock));

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
