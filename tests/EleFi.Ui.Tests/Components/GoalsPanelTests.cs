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

        var ledger = Substitute.For<ITransactionRepository>();
        ledger.BalancesAsync(Arg.Any<CancellationToken>()).Returns(
            [new ContainerBalance(_rd.Id, _rd.Name, _rd.Kind, Money.FromMinor(1_50_000_00, Currency.Inr))]);

        var containers = Substitute.For<IContainerRepository>();
        containers.ListSelectableAsync(Arg.Any<CancellationToken>()).Returns([_rd]);

        Services.AddSingleton<IClock>(_clock);
        Services.AddSingleton(new GoalService(repository, ledger, _clock));
        Services.AddSingleton(containers);
        Services.AddSingleton(new ToastService());
    }

    [Fact]
    public void A_goal_made_in_the_sheet_shows_its_progress_and_status()
    {
        var panel = RenderComponent<GoalsPanel>();

        panel.Find("#new-goal").Click();
        panel.Find("#goal-name").Change("Ring");
        panel.Find("#goal-target").Change("300000");
        panel.Find("#mode-balance").Click();
        panel.FindAll("#goal-containers .chip")[0].Click();
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

        panel.Find("#new-goal").Click();
        panel.Find("#goal-name").Change("Ring");
        panel.Find("#goal-target").Change("300000");
        panel.Find("#mode-balance").Click();
        panel.Find("[role=dialog] button.primary").Click();

        Assert.Empty(_goals);
        Assert.Contains("container", panel.Find("[role=alert]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_money_records_a_transaction_toward_a_contributions_goal()
    {
        _goals.Add(new Goal
        {
            Name = "Trip", TargetAmountMinor = 50_000_00, StartDate = _clock.Today, TargetDate = _clock.Today.AddMonths(6),
            FundingMode = FundingMode.TracksContributions,
        });
        var panel = RenderComponent<GoalsPanel>();

        panel.Find(".goal-add").Click();

        var uri = Services.GetRequiredService<NavigationManager>().Uri;
        Assert.Contains($"capture?goal={_goals[0].Id}", uri, StringComparison.Ordinal);
    }
}
