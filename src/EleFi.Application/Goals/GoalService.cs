using EleFi.Application.Abstractions;
using EleFi.Domain.Balances;
using EleFi.Domain.Goals;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Goals;

/// <summary>What the goal form gathered.</summary>
/// <param name="Name">What it is for.</param>
/// <param name="TargetAmountMinor">The target, in minor units.</param>
/// <param name="TargetDate">When it should be reached.</param>
/// <param name="FundingMode">How progress is measured.</param>
/// <param name="ContainerIds">Linked containers. At least one in balance mode.</param>
/// <param name="OpeningAllocationMinor">Money already saved, in contributions mode.</param>
/// <param name="MonthlyContributionMinor">What the user means to put aside each month, if anything.</param>
/// <param name="StartDate">When saving started. Today when not given.</param>
public sealed record GoalDraft(
    string Name,
    long TargetAmountMinor,
    DateOnly TargetDate,
    FundingMode FundingMode,
    IReadOnlyList<Guid> ContainerIds,
    long OpeningAllocationMinor = 0,
    long? MonthlyContributionMinor = null,
    DateOnly? StartDate = null);

/// <summary>A saved goal, or why it could not be saved.</summary>
/// <param name="Goal">The goal, when it worked.</param>
/// <param name="Error">What to fix, when it did not.</param>
public sealed record GoalResult(Goal? Goal, string? Error)
{
    /// <summary>True when it was saved.</summary>
    public bool Succeeded => Error is null;
}

/// <summary>A goal with where it stands today.</summary>
/// <param name="Goal">The goal.</param>
/// <param name="Standing">Progress, status and what it still needs.</param>
/// <param name="ContainerNames">The names of its linked containers.</param>
public sealed record GoalView(Goal Goal, GoalStanding Standing, IReadOnlyList<string> ContainerNames);

/// <summary>
/// Goals: creating them, measuring them, and attributing transactions to them.
/// </summary>
/// <remarks>
/// Progress is derived on every read and never stored, and it never counts toward net worth
/// (GL3). In balance mode it is the combined balance of the linked containers; in
/// contributions mode it is the opening allocation plus every transaction attributed to it.
/// </remarks>
/// <param name="goals">Where goals live.</param>
/// <param name="transactions">The ledger, for balances.</param>
/// <param name="clock">Today.</param>
public sealed class GoalService(IGoalRepository goals, ITransactionRepository transactions, IClock clock)
{
    /// <summary>Every goal with where it stands: active ones by target date, then archived.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<GoalView>> OverviewAsync(CancellationToken cancellationToken = default)
    {
        var all = await goals.ListAsync(cancellationToken).ConfigureAwait(false);
        if (all.Count == 0)
        {
            return [];
        }

        var balances = await transactions.BalancesAsync(cancellationToken).ConfigureAwait(false);
        var contributions = await ContributionTotalsAsync(cancellationToken).ConfigureAwait(false);
        var today = clock.Today;

        return all
            .OrderBy(g => g.IsArchived)
            .ThenBy(g => g.TargetDate)
            .Select(g =>
            {
                var names = balances
                    .Where(b => g.Containers.Any(c => c.ContainerId == b.ContainerId))
                    .Select(b => b.Name)
                    .ToList();
                return new GoalView(g, GoalMath.Stand(g, Progress(g, balances, contributions), today), names);
            })
            .ToList();
    }

    /// <summary>
    /// Containers whose contributions-mode goals together claim more than they hold (GL5).
    /// </summary>
    /// <remarks>A warning, never a block: over-allocating is something people do knowingly.</remarks>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<string>> WarningsAsync(CancellationToken cancellationToken = default)
    {
        var all = await goals.ListAsync(cancellationToken).ConfigureAwait(false);
        var drawing = all.Where(g => !g.IsArchived && g.FundingMode == FundingMode.TracksContributions).ToList();
        if (drawing.Count == 0)
        {
            return [];
        }

        var balances = await transactions.BalancesAsync(cancellationToken).ConfigureAwait(false);
        var contributions = await ContributionTotalsAsync(cancellationToken).ConfigureAwait(false);
        var warnings = new List<string>();

        foreach (var balance in balances)
        {
            var claiming = drawing.Where(g => g.Containers.Any(c => c.ContainerId == balance.ContainerId)).ToList();
            if (claiming.Count == 0)
            {
                continue;
            }

            var claimed = claiming.Sum(g => Progress(g, balances, contributions));
            if (claimed > balance.Balance.Minor)
            {
                var held = MoneyText.ToDisplayString(balance.Balance);
                var claim = MoneyText.ToDisplayString(Money.SignedMinor(claimed, balance.Balance.Currency));
                warnings.Add($"Goals drawing on {balance.Name} claim {claim}, but it holds {held}.");
            }
        }

        return warnings;
    }

    /// <summary>One goal, or null.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<Goal?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        goals.FindAsync(id, cancellationToken);

    /// <summary>Goals a transaction can be attributed to: live, not archived, contributions mode (T8).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<Goal>> AttributableAsync(CancellationToken cancellationToken = default)
    {
        var all = await goals.ListAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(g => !g.IsArchived && g.FundingMode == FundingMode.TracksContributions).ToList();
    }

    /// <summary>Transactions attributed to one goal, newest first.</summary>
    /// <param name="goalId">The goal.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<Transaction>> ContributionsAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var all = await goals.ContributionsAsync(cancellationToken).ConfigureAwait(false);
        return all
            .Where(t => t.GoalId == goalId)
            .OrderByDescending(t => t.OccurredOn)
            .ThenByDescending(t => t.OccurredAtTime)
            .ToList();
    }

    /// <summary>Creates a goal.</summary>
    /// <param name="draft">What the form gathered.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<GoalResult> CreateAsync(GoalDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var start = draft.StartDate ?? clock.Today;
        if (Validate(draft, start) is { } error)
        {
            return new GoalResult(null, error);
        }

        var now = clock.UtcNow;
        var goal = new Goal { CreatedAt = now, UpdatedAt = now };
        Apply(goal, draft, start);
        goal.Containers = [.. draft.ContainerIds.Distinct().Select(id => new GoalContainer { GoalId = goal.Id, ContainerId = id })];

        await goals.AddAsync(goal, cancellationToken).ConfigureAwait(false);
        return new GoalResult(goal, null);
    }

    /// <summary>Changes a goal.</summary>
    /// <param name="id">The goal.</param>
    /// <param name="draft">What the form gathered.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<GoalResult> UpdateAsync(Guid id, GoalDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var goal = await goals.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (goal is null)
        {
            return new GoalResult(null, "That goal is no longer here.");
        }

        var start = draft.StartDate ?? goal.StartDate;
        if (Validate(draft, start) is { } error)
        {
            return new GoalResult(null, error);
        }

        Apply(goal, draft, start);
        await goals.UpdateAsync(goal, [.. draft.ContainerIds.Distinct()], cancellationToken).ConfigureAwait(false);
        return new GoalResult(goal, null);
    }

    /// <summary>Archives a goal, or brings it back. Its transactions are kept either way (FR-6.12).</summary>
    /// <param name="id">The goal.</param>
    /// <param name="archived">True to archive.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task ArchiveAsync(Guid id, bool archived, CancellationToken cancellationToken = default)
    {
        var goal = await goals.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (goal is null)
        {
            return;
        }

        goal.IsArchived = archived;
        await goals.UpdateAsync(goal, [.. goal.Containers.Select(c => c.ContainerId)], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes a goal and clears it from its transactions (GL6).</summary>
    /// <param name="id">The goal.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        goals.DeleteAsync(id, cancellationToken);

    /// <summary>
    /// Attributes a transaction to a goal, or clears it with null. Only a contributions-mode
    /// goal takes contributions (T8).
    /// </summary>
    /// <param name="transactionId">The transaction.</param>
    /// <param name="goalId">The goal, or null for none.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Null when done, or why it could not be.</returns>
    public async Task<string?> AttributeAsync(Guid transactionId, Guid? goalId, CancellationToken cancellationToken = default)
    {
        if (goalId is { } id)
        {
            var goal = await goals.FindAsync(id, cancellationToken).ConfigureAwait(false);
            if (goal is null)
            {
                return "That goal is no longer here.";
            }

            if (goal.FundingMode != FundingMode.TracksContributions)
            {
                return $"\"{goal.Name}\" follows its containers' balance, so transactions are not added to it.";
            }
        }

        await goals.SetGoalAsync(transactionId, goalId, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private static long Progress(Goal goal, IReadOnlyList<ContainerBalance> balances, IReadOnlyDictionary<Guid, long> contributions)
    {
        if (goal.FundingMode == FundingMode.TracksContainerBalance)
        {
            // Same currency only: a goal in rupees does not silently add a dollar balance.
            return balances
                .Where(b => goal.Containers.Any(c => c.ContainerId == b.ContainerId)
                    && string.Equals(b.Balance.Currency.Code, goal.CurrencyCode, StringComparison.Ordinal))
                .Sum(b => b.Balance.Minor);
        }

        return goal.OpeningAllocationMinor + contributions.GetValueOrDefault(goal.Id);
    }

    private static string? Validate(GoalDraft draft, DateOnly start)
    {
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            return "Give the goal a name.";
        }

        if (draft.TargetAmountMinor <= 0)
        {
            return "The target has to be more than zero.";
        }

        if (draft.TargetDate <= start)
        {
            return "The target date has to be after the start date.";
        }

        if (draft.FundingMode == FundingMode.TracksContainerBalance && draft.ContainerIds.Count == 0)
        {
            return "Pick at least one container whose balance this goal follows.";
        }

        if (draft.OpeningAllocationMinor < 0 || draft.MonthlyContributionMinor < 0)
        {
            return "Amounts cannot be negative.";
        }

        return null;
    }

    private static void Apply(Goal goal, GoalDraft draft, DateOnly start)
    {
        goal.Name = draft.Name.Trim();
        goal.TargetAmountMinor = draft.TargetAmountMinor;
        goal.TargetDate = draft.TargetDate;
        goal.StartDate = start;
        goal.FundingMode = draft.FundingMode;
        goal.MonthlyContributionMinor = draft.MonthlyContributionMinor;
        goal.OpeningAllocationMinor = draft.FundingMode == FundingMode.TracksContributions ? draft.OpeningAllocationMinor : 0;
    }

    private async Task<IReadOnlyDictionary<Guid, long>> ContributionTotalsAsync(CancellationToken cancellationToken)
    {
        var all = await goals.ContributionsAsync(cancellationToken).ConfigureAwait(false);
        return all
            .Where(t => t.GoalId is not null)
            .GroupBy(t => t.GoalId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(GoalMath.ContributionOf));
    }
}
