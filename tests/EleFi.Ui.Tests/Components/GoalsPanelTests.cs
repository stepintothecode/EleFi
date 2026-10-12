using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Goals;
using EleFi.Domain.Balances;
using EleFi.Domain.Containers;
using EleFi.Domain.Goals;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;
using EleFi.Ui.Components;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components;

/// <summary>The Goals half of the Plans page.</summary>
public class GoalsPanelTests : Bunit.TestContext
{
    private readonly List<Goal> _goals = [];
    private readonly Fakes.StoppedClock _clock = new();
    private readonly Container _rd = new() { Name = "Ring RD", Kind = ContainerKind.RecurringDeposit };

    public GoalsPanelTests()
    {
        var repository = Substitute.For<IGoalRepository>();
        repository.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => _goals.Where(g => g.DeletedAt is null).ToList());
        repository.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => _goals.Find(g => g.Id == call.Arg<Guid>()));
        repository.When(r => r.AddAsync(Arg.Any<Goal>(), Arg.Any<CancellationToken>())).Do(call => _goals.Add(call.Arg<Goal>()));
        repository.ContributionsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Transaction>());
        repository.When(r => r.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(call => _goals.Find(g => g.Id == call.Arg<Guid>())!.DeletedAt = DateTimeOffset.UnixEpoch);
        repository.When(r => r.RestoreAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(call => _goals.Find(g => g.Id == call.Arg<Guid>())!.DeletedAt = null);

        var ledger = Substitute.For<ITransactionRepository>();
        ledger.BalancesAsync(Arg.Any<CancellationToken>()).Returns(
            [new ContainerBalance(_rd.Id, _rd.Name, _rd.Kind, Money.FromMinor(1_50_000_00, Currency.Inr))]);

        var containers = Substitute.For<IContainerRepository>();
        containers.ListSelectableAsync(Arg.Any<CancellationToken>()).Returns([_rd]);

        Services.AddSingleton<IClock>(_clock);
        Services.AddSingleton(new GoalService(repository, ledger, _clock));
        Services.AddSingleton(containers);
        Services.AddEleAndUndo();
    }

    [Fact]
    public void A_goal_made_in_the_sheet_shows_its_progress_and_status()
    {
        var panel = RenderComponent<GoalsPanel>();

        panel.InvokeAsync(panel.Instance.StartCreate);
        panel.Find("#goal-name").Change("Ring");
        panel.Find("#goal-target").Change("300000");
        panel.Find("#mode-balance").Click();
        panel.FindAll("#goal-containers .goal-pick")[0].Click();
        panel.Find("[role=dialog] button.primary").Click();

        var goal = Assert.Single(_goals);
        Assert.Equal(FundingMode.TracksContainerBalance, goal.FundingMode);

        // ₹1,50,000 of ₹3,00,000 on the first day: half way, and ahead of the line.
        Assert.Equal("50", panel.Find(".goal-bar").GetAttribute("aria-valuenow"));
        Assert.Equal("On track", panel.Find(".goal-status").TextContent);
        Assert.Contains("Follows Ring RD", panel.Find(".goal-card").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Following_a_balance_with_no_container_is_explained_and_not_saved()
    {
        var panel = RenderComponent<GoalsPanel>();

        panel.InvokeAsync(panel.Instance.StartCreate);
        panel.Find("#goal-name").Change("Ring");
        panel.Find("#goal-target").Change("300000");
        panel.Find("#mode-balance").Click();
        panel.Find("[role=dialog] button.primary").Click();

        Assert.Empty(_goals);
        var ele = Services.GetRequiredService<EleReactions>().Current!;
        Assert.Equal(EleTone.Cross, ele.Tone);
        Assert.Contains("container", ele.Line, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_money_records_a_transaction_toward_a_contributions_goal()
    {
        _goals.Add(new Goal
        {
            Name = "Trip",
            TargetAmountMinor = 50_000_00,
            StartDate = _clock.Today,
            TargetDate = _clock.Today.AddMonths(6),
            FundingMode = FundingMode.TracksContributions,
        });
        var panel = RenderComponent<GoalsPanel>();

        panel.Find(".goal-add").Click();

        var uri = Services.GetRequiredService<NavigationManager>().Uri;
        Assert.Contains($"capture?goal={_goals[0].Id}", uri, StringComparison.Ordinal);
    }

    [Fact]
    public void A_goal_with_a_planned_monthly_amount_says_when_it_will_be_done()
    {
        _goals.Add(new Goal
        {
            Name = "Laptop",
            TargetAmountMinor = 1_20_000_00,
            TargetDate = _clock.Today.AddYears(2),
            FundingMode = FundingMode.TracksContributions,
            MonthlyContributionMinor = 10_000_00,
            StartDate = _clock.Today,
        });

        var panel = RenderComponent<GoalsPanel>();

        var line = panel.Find(".goal-projection").TextContent;
        Assert.StartsWith("At your planned ₹10,000.00 a month, done by", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Deleting_a_goal_can_be_undone_from_the_arrow()
    {
        _goals.Add(new Goal { Name = "Trip", TargetAmountMinor = 50_000_00, TargetDate = _clock.Today.AddYears(1), StartDate = _clock.Today });
        var panel = RenderComponent<GoalsPanel>();

        panel.Find("button[aria-label='Edit Trip']").Click();
        panel.Find("[role=dialog] button.danger").Click();
        panel.WaitForAssertion(() => Assert.Empty(panel.FindAll("button[aria-label='Edit Trip']")));

        // The arrows live in the Plans page's top bar; here the step they would run is checked.
        var undo = Services.GetRequiredService<UndoHistory>();
        Assert.True(undo.CanUndo(UndoHistory.Goals));
        panel.InvokeAsync(async () =>
        {
            await undo.UndoAsync(UndoHistory.Goals);
            await panel.Instance.ReloadAsync();
        });

        panel.WaitForAssertion(() => Assert.NotNull(panel.Find("button[aria-label='Edit Trip']")));
        Assert.Empty(panel.FindAll(".goals-head button.undo"));
    }
}
