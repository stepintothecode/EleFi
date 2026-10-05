using EleFi.Application.Abstractions;
using EleFi.Application.Goals;
using EleFi.Application.Tests.Support;
using EleFi.Domain.Balances;
using EleFi.Domain.Containers;
using EleFi.Domain.Goals;
using EleFi.Domain.Money;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;
using NSubstitute;

namespace EleFi.Application.Tests.Goals;

/// <summary>Goals: the rules that keep them honest, and how progress is worked out.</summary>
public class GoalServiceTests
{
    private readonly MovableClock _clock = new();
    private readonly FakeGoals _goals = new();
    private readonly ITransactionRepository _ledger = Substitute.For<ITransactionRepository>();
    private readonly Guid _rd = Guid.NewGuid();
    private readonly Guid _salary = Guid.NewGuid();

    public GoalServiceTests()
    {
        _ledger.BalancesAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new ContainerBalance(_rd, "Ring RD", ContainerKind.RecurringDeposit, Money.FromMinor(60_000_00, Currency.Inr)),
            new ContainerBalance(_salary, "SBI", ContainerKind.BankAccount, Money.FromMinor(1_00_000_00, Currency.Inr)),
        ]);
    }

    private GoalService Service => new(_goals, _ledger, _clock);

    [Fact]
    public async Task A_goal_that_follows_a_container_has_its_balance_as_progress()
    {
        var created = await Service.CreateAsync(Draft(FundingMode.TracksContainerBalance, [_rd]));

        var view = Assert.Single(await Service.OverviewAsync());

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(60_000_00, view.Standing.ProgressMinor);
        Assert.Equal(["Ring RD"], view.ContainerNames);
    }

    [Fact]
    public async Task A_contributions_goal_counts_its_opening_allocation_plus_what_was_put_in_minus_what_was_spent()
    {
        var goal = (await Service.CreateAsync(Draft(FundingMode.TracksContributions, [], opening: 20_000_00))).Goal!;
        _goals.Contributions.Add(Transfer(10_000_00, goal.Id));
        _goals.Contributions.Add(Spend(3_000_00, goal.Id));

        var view = Assert.Single(await Service.OverviewAsync());

        Assert.Equal(27_000_00, view.Standing.ProgressMinor);
    }

    [Theory]
    [InlineData("", 100_00, "name")]
    [InlineData("Ring", 0, "more than zero")]
    public async Task A_goal_needs_a_name_and_a_positive_target(string name, long target, string error)
    {
        var result = await Service.CreateAsync(Draft(FundingMode.TracksContributions, []) with { Name = name, TargetAmountMinor = target });

        Assert.False(result.Succeeded);
        Assert.Contains(error, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GL1_the_target_date_must_be_after_the_start()
    {
        var result = await Service.CreateAsync(Draft(FundingMode.TracksContributions, []) with { TargetDate = _clock.Today });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GL4_following_a_balance_needs_a_container()
    {
        var result = await Service.CreateAsync(Draft(FundingMode.TracksContainerBalance, []));

        Assert.False(result.Succeeded);
        Assert.Contains("container", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task T8_only_a_contributions_goal_takes_transactions()
    {
        var follows = (await Service.CreateAsync(Draft(FundingMode.TracksContainerBalance, [_rd]))).Goal!;
        var counts = (await Service.CreateAsync(Draft(FundingMode.TracksContributions, []))).Goal!;
        var spend = Guid.NewGuid();

        Assert.NotNull(await Service.AttributeAsync(spend, follows.Id));
        Assert.Null(await Service.AttributeAsync(spend, counts.Id));
        Assert.Equal(counts.Id, _goals.Attributed[spend]);
    }

    [Fact]
    public async Task GL5_goals_claiming_more_than_their_container_holds_raise_a_warning_not_a_block()
    {
        await Service.CreateAsync(Draft(FundingMode.TracksContributions, [_salary], opening: 70_000_00));
        var second = await Service.CreateAsync(Draft(FundingMode.TracksContributions, [_salary], opening: 40_000_00));

        var warning = Assert.Single(await Service.WarningsAsync());

        Assert.True(second.Succeeded);
        Assert.Contains("SBI", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_archived_goal_is_listed_after_the_active_ones_and_takes_no_contributions()
    {
        var old = (await Service.CreateAsync(Draft(FundingMode.TracksContributions, []) with { Name = "Old" })).Goal!;
        await Service.CreateAsync(Draft(FundingMode.TracksContributions, []) with { Name = "New" });

        await Service.ArchiveAsync(old.Id, archived: true);

        Assert.Equal(["New", "Old"], (await Service.OverviewAsync()).Select(v => v.Goal.Name));
        Assert.DoesNotContain(await Service.AttributableAsync(), g => g.Id == old.Id);
    }

    private GoalDraft Draft(FundingMode mode, IReadOnlyList<Guid> containers, long opening = 0) =>
        new("Ring", 3_00_000_00, _clock.Today.AddYears(2), mode, containers, opening);

    private static Transaction Transfer(long minor, Guid goal) => Make(Mine(), Mine(), minor, goal);

    private static Transaction Spend(long minor, Guid goal) =>
        Make(Mine(), new Party { Kind = PartyKind.External, Name = "Tanishq" }, minor, goal);

    private static Party Mine() => new() { Kind = PartyKind.Container, ContainerId = Guid.NewGuid() };

    private static Transaction Make(Party source, Party destination, long minor, Guid goal) => new()
    {
        SourceParty = source,
        SourcePartyId = source.Id,
        DestinationParty = destination,
        DestinationPartyId = destination.Id,
        SourceAmountMinor = minor,
        DestinationAmountMinor = minor,
        GoalId = goal,
    };

    private sealed class FakeGoals : IGoalRepository
    {
        public List<Goal> Rows { get; } = [];

        public List<Transaction> Contributions { get; } = [];

        public Dictionary<Guid, Guid?> Attributed { get; } = [];

        public Task<IReadOnlyList<Goal>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Goal>>(Rows.Where(g => g.DeletedAt is null).ToList());

        public Task<Goal?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.Find(g => g.Id == id && g.DeletedAt is null));

        public Task AddAsync(Goal goal, CancellationToken cancellationToken = default)
        {
            Rows.Add(goal);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Goal goal, IReadOnlyCollection<Guid> containerIds, CancellationToken cancellationToken = default)
        {
            goal.Containers = [.. containerIds.Select(id => new GoalContainer { GoalId = goal.Id, ContainerId = id })];
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Rows.Find(g => g.Id == id)!.DeletedAt = DateTimeOffset.UnixEpoch;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Transaction>> ContributionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>(Contributions);

        public Task SetGoalAsync(Guid transactionId, Guid? goalId, CancellationToken cancellationToken = default)
        {
            Attributed[transactionId] = goalId;
            return Task.CompletedTask;
        }
    }
}
