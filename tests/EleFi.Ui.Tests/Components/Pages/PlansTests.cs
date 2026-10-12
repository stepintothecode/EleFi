using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Goals;
using EleFi.Application.Planning;
using EleFi.Domain.Containers;
using EleFi.Domain.Goals;
using EleFi.Domain.Parties;
using EleFi.Domain.Planning;
using EleFi.Domain.Transactions;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>The Plans page: a to-do list with money in it.</summary>
public class PlansTests : Bunit.TestContext
{
    private readonly List<Plan> _plans = [];
    private readonly ITransactionRepository _ledger = Substitute.For<ITransactionRepository>();
    private readonly Fakes.StoppedClock _clock = new();
    private readonly Container _sbi = new() { Name = "SBI", Kind = ContainerKind.BankAccount };
    private readonly Fakes.SeededLabels _labels = new();

    public PlansTests()
    {
        var repository = Substitute.For<IPlanRepository>();
        repository.ListOpenAsync(Arg.Any<CancellationToken>()).Returns(_ => _plans.Where(p => !p.IsDone && p.DeletedAt is null).OrderBy(p => p.DueOn).ToList());
        repository.ListCompletedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(_ => _plans.Where(p => p.IsDone).ToList());
        repository.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => _plans.Find(p => p.Id == call.Arg<Guid>()));
        repository.When(r => r.AddAsync(Arg.Any<Plan>(), Arg.Any<CancellationToken>())).Do(call => _plans.Add(call.Arg<Plan>()));
        repository.ClaimedTransactionIdsAsync(Arg.Any<CancellationToken>()).Returns(new HashSet<Guid>());
        repository.When(r => r.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())).Do(call => _plans.RemoveAll(p => p.Id == call.Arg<Guid>()));

        var containers = Substitute.For<IContainerRepository>();
        containers.ListSelectableAsync(Arg.Any<CancellationToken>()).Returns([_sbi]);
        var parties = Substitute.For<IPartyRepository>();
        parties.ListExternalAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Party>());

        Services.AddSingleton<IClock>(_clock);
        Services.AddSingleton(new PlanService(repository, _ledger, _clock, Substitute.For<IAuditRepository>(), Substitute.For<IReminderSchedule>()));
        Services.AddSingleton(containers);
        Services.AddSingleton(parties);
        Services.AddEleAndUndo();
        Services.AddSingleton(Substitute.For<IAlertAccess>());
        Services.AddSingleton(new EleFi.Application.Labels.LabelService(_labels, _clock));

        var goals = Substitute.For<IGoalRepository>();
        goals.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Goal>());
        Services.AddSingleton(new GoalService(goals, _ledger, _clock));
    }

    [Fact]
    public void Plans_are_grouped_by_when_they_are_due()
    {
        _plans.Add(new Plan { Title = "Card bill", DueOn = _clock.Today.AddDays(-2) });
        _plans.Add(new Plan { Title = "Rent", DueOn = _clock.Today });
        _plans.Add(new Plan { Title = "Insurance", DueOn = _clock.Today.AddDays(20) });

        var page = RenderComponent<Plans>();

        Assert.Equal(["Overdue", "Today", "Later"], page.FindAll("h2.plan-group").Select(h => h.TextContent));
    }

    [Fact]
    public void A_new_plan_is_made_in_a_sheet_and_lands_on_the_list()
    {
        var page = RenderComponent<Plans>();

        page.Find(".section-head button.primary").Click();
        page.Find("#plan-title").Change("SIP, Axis Bluechip");
        page.Find("#plan-amount").Change("5000");
        page.Find("#plan-repeat").Change(RepeatFrequency.Monthly.ToString());
        page.Find("[role=dialog] button.primary").Click();

        var plan = Assert.Single(_plans);
        Assert.Equal(500000, plan.AmountMinor);
        Assert.Equal(RepeatFrequency.Monthly, plan.RepeatFrequency);
        Assert.Contains("SIP, Axis Bluechip", page.Find(".plan-list").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_form_uses_the_capture_forms_words_never_to_and_from()
    {
        var page = RenderComponent<Plans>();

        page.Find(".section-head button.primary").Click();

        Assert.StartsWith("Paid from", page.Find("label[for=plan-from]").TextContent, StringComparison.Ordinal);
        Assert.StartsWith("Paid to", page.Find("label[for=plan-who]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Ticking_a_plan_with_no_amount_marks_it_done_and_moves_it_to_completed()
    {
        _plans.Add(new Plan { Title = "Call the bank", DueOn = _clock.Today });
        var page = RenderComponent<Plans>();

        page.Find("button[aria-label='Mark Call the bank done']").Click();

        Assert.True(_plans[0].IsDone);
        Assert.Contains("Completed (1)", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Ticking_a_plan_nothing_matches_offers_to_record_it_through_the_capture_form()
    {
        _plans.Add(new Plan { Title = "SIP", AmountMinor = 500000, ContainerId = _sbi.Id, DueOn = _clock.Today });
        var page = RenderComponent<Plans>();

        page.Find("button[aria-label='Mark SIP done']").Click();
        page.FindAll("[role=dialog] button.primary").First(b => b.TextContent.Contains("Record", StringComparison.Ordinal)).Click();

        var uri = Services.GetRequiredService<NavigationManager>().Uri;
        Assert.Contains("capture?plan=", uri, StringComparison.Ordinal);
        Assert.Contains("amount=5000", uri, StringComparison.Ordinal);
        Assert.Contains($"container={_sbi.Id}", uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Ticking_a_plan_with_exactly_one_matching_transaction_links_it_straight_away()
    {
        var sip = new Plan { Title = "SIP", AmountMinor = 500000, DueOn = _clock.Today };
        _plans.Add(sip);
        var mine = new Party { Kind = PartyKind.Container, ContainerId = _sbi.Id };
        var fund = new Party { Kind = PartyKind.External, Name = "Fund" };
        var paid = new Transaction
        {
            SourceParty = mine,
            SourcePartyId = mine.Id,
            DestinationParty = fund,
            DestinationPartyId = fund.Id,
            SourceAmountMinor = 500000,
            DestinationAmountMinor = 500000,
            OccurredOn = _clock.Today,
        };
        _ledger.QueryAsync(Arg.Any<Domain.Filters.TransactionFilter>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([paid]);
        var page = RenderComponent<Plans>();

        page.Find("button[aria-label='Mark SIP done']").Click();

        Assert.Equal(paid.Id, sip.TransactionId);
        Assert.Empty(page.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Goals_have_a_place_on_the_same_page()
    {
        var page = RenderComponent<Plans>();

        page.FindAll(".segmented button")[1].Click();

        Assert.Contains("No goals yet", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void On_goals_the_undo_arrows_sit_in_the_top_bar_beside_new_goal_as_on_plans()
    {
        var page = RenderComponent<Plans>();
        Assert.NotNull(page.Find(".section-head button.undo"));

        page.FindAll(".segmented button")[1].Click();

        var head = page.Find(".section-head");
        Assert.NotNull(head.QuerySelector("button.undo"));
        Assert.NotNull(head.QuerySelector("#new-goal"));
        Assert.Single(page.FindAll("button.undo"));
    }

    [Fact]
    public void A_weekly_plan_can_fall_on_chosen_days()
    {
        var page = RenderComponent<Plans>();

        page.Find(".section-head button.primary").Click();
        page.Find("#plan-title").Change("Gym");
        page.Find("#plan-repeat").Change(RepeatFrequency.Weekly.ToString());
        page.FindAll(".weekday-chips .chip").First(c => c.TextContent == "Mon").Click();
        page.FindAll(".weekday-chips .chip").First(c => c.TextContent == "Thu").Click();
        page.Find("[role=dialog] button.primary").Click();

        var plan = Assert.Single(_plans);
        Assert.Equal(RepeatDays.Monday | RepeatDays.Thursday, plan.RepeatDays);
        Assert.Contains("Mon, Thu", page.Find(".plan-list").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_monthly_plan_can_be_on_the_last_day_and_end_after_a_number_of_times()
    {
        var page = RenderComponent<Plans>();

        page.Find(".section-head button.primary").Click();
        page.Find("#plan-title").Change("Rent");
        page.Find("#plan-repeat").Change(RepeatFrequency.Monthly.ToString());
        page.Find("#plan-last-day").Change(true);
        page.Find("#plan-ends").Change("count");
        page.Find("#plan-count").Change("11");
        page.Find("[role=dialog] button.primary").Click();

        var plan = Assert.Single(_plans);
        Assert.Equal(RepeatRule.LastDay, plan.AnchorDay);
        Assert.Equal(11, plan.RepeatCount);
        Assert.Contains("1 of 11", page.Find(".plan-list").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Skipping_rolls_a_repeating_plan_on_and_the_undo_arrow_takes_it_back()
    {
        var rent = new Plan { Title = "Rent", DueOn = _clock.Today, RepeatFrequency = RepeatFrequency.Monthly, AnchorDay = _clock.Today.Day };
        _plans.Add(rent);
        var page = RenderComponent<Plans>();

        page.Find(".plan-row .plan-body").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[role=dialog] button.skip-plan")));
        page.Find("[role=dialog] button.skip-plan").Click();

        page.WaitForAssertion(() => Assert.True(rent.IsSkipped));
        Assert.Contains(_plans, p => p.Id != rent.Id && p.DueOn == _clock.Today.AddMonths(1));
        Assert.Contains("Skipped", page.Markup, StringComparison.Ordinal);

        page.Find(".section-head button.undo").Click();

        page.WaitForAssertion(() => Assert.False(rent.IsSkipped));
        Assert.Single(_plans);
    }

    [Fact]
    public void Mark_done_on_a_reminder_opens_the_page_ticking_that_plan()
    {
        var call = new Plan { Title = "Call the bank", DueOn = _clock.Today };
        _plans.Add(call);
        Services.GetRequiredService<NavigationManager>().NavigateTo($"plans?tick={call.Id}");

        RenderComponent<Plans>();

        Assert.True(call.IsDone);
    }

    [Fact]
    public async Task Recording_a_plan_carries_its_labels_to_the_capture_form()
    {
        var food = (await _labels.ListAsync())[0];
        var groceries = new Plan { Title = "Groceries", AmountMinor = 2_000_00, DueOn = _clock.Today };
        groceries.Labels.Add(new PlanLabel { PlanId = groceries.Id, LabelId = food.Id });
        _plans.Add(groceries);
        var page = RenderComponent<Plans>();

        page.Find("button[aria-label='Mark Groceries done']").Click();
        page.FindAll("[role=dialog] button.primary").First(b => b.TextContent.Contains("Record", StringComparison.Ordinal)).Click();

        Assert.Contains($"labels={food.Id}", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }
}
