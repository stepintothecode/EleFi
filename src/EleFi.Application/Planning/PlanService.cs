using EleFi.Application.Abstractions;
using EleFi.Domain.Filters;
using EleFi.Domain.Planning;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Planning;

/// <summary>What the plan form gathered.</summary>
/// <param name="Title">What it is.</param>
/// <param name="DueOn">When it is due.</param>
/// <param name="AmountMinor">The amount expected, if fixed.</param>
/// <param name="Direction">Money leaving, arriving, or moving between the user's containers.</param>
/// <param name="ContainerId">The user's container at the plan's end.</param>
/// <param name="CounterpartyName">Who is paid or pays, for a Debit or Credit.</param>
/// <param name="DestinationContainerId">The container money moves into, for a transfer.</param>
/// <param name="Repeat">How often it comes round.</param>
/// <param name="DueTime">The time it is due, if it matters.</param>
/// <param name="Note">Anything else.</param>
public sealed record PlanDraft(
    string Title,
    DateOnly DueOn,
    long? AmountMinor = null,
    TransactionKind Direction = TransactionKind.Debit,
    Guid? ContainerId = null,
    string? CounterpartyName = null,
    Guid? DestinationContainerId = null,
    RepeatRule Repeat = default,
    TimeOnly? DueTime = null,
    string? Note = null);

/// <summary>The outcome of a plan change.</summary>
/// <param name="Plan">The plan, when it succeeded.</param>
/// <param name="Error">Why it was refused, in words the user can act on.</param>
public readonly record struct PlanResult(Plan? Plan, string? Error)
{
    /// <summary>True when the change was saved.</summary>
    public bool Succeeded => Error is null;
}

/// <summary>
/// The Planner: money the user expects to move, and ticking it off against the ledger.
/// </summary>
/// <remarks>
/// <para>
/// A plan never writes money. Ticking one off links it to a transaction that already exists
/// (recorded by hand, or by an SMS or a payment app), or the user records that transaction
/// through the ordinary capture form and the plan is linked to the result. So no plan can
/// put money in the ledger that nothing confirmed happened.
/// </para>
/// <para>
/// When an alert records a transaction that matches an open plan,
/// <see cref="CompleteMatchingAsync"/> ticks the plan off on its own: the SIP whose SMS came
/// needs nothing from the user, and the one whose SMS never came stays open on its due date.
/// </para>
/// </remarks>
public sealed class PlanService(IPlanRepository plans, ITransactionRepository transactions, IClock clock)
{
    /// <summary>Plans still to do, soonest first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IReadOnlyList<Plan>> OpenAsync(CancellationToken cancellationToken = default) =>
        plans.ListOpenAsync(cancellationToken);

    /// <summary>Plans done, most recently first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IReadOnlyList<Plan>> CompletedAsync(CancellationToken cancellationToken = default) =>
        plans.ListCompletedAsync(100, cancellationToken);

    /// <summary>One plan, or null.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<Plan?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        plans.FindAsync(id, cancellationToken);

    /// <summary>Creates a plan.</summary>
    /// <param name="draft">What the form gathered.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<PlanResult> CreateAsync(PlanDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (Validate(draft) is { } error)
        {
            return new PlanResult(null, error);
        }

        var now = clock.UtcNow;
        var plan = new Plan { CreatedAt = now };
        plan.SeriesId = plan.Id;
        Apply(plan, draft, now);

        await plans.AddAsync(plan, cancellationToken).ConfigureAwait(false);
        return new PlanResult(plan, null);
    }

    /// <summary>Changes an open plan.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="draft">What the form gathered.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<PlanResult> UpdateAsync(Guid id, PlanDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (Validate(draft) is { } error)
        {
            return new PlanResult(null, error);
        }

        var plan = await plans.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return new PlanResult(null, "That plan no longer exists.");
        }

        Apply(plan, draft, clock.UtcNow);
        await plans.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
        return new PlanResult(plan, null);
    }

    /// <summary>
    /// The transactions already in the ledger that could be this plan, nearest first.
    /// </summary>
    /// <remarks>
    /// Empty for a plan with no amount: without one there is nothing to match on, and
    /// guessing would link the wrong money.
    /// </remarks>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<Transaction>> CandidatesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await plans.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (plan?.AmountMinor is not { } amount)
        {
            return [];
        }

        var near = await transactions.QueryAsync(
            new TransactionFilter
            {
                From = plan.DueOn.AddDays(-PlanMatcher.WindowDays),
                To = plan.DueOn.AddDays(PlanMatcher.WindowDays),
                MinAmountMinor = amount,
                MaxAmountMinor = amount,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var claimed = await plans.ClaimedTransactionIdsAsync(cancellationToken).ConfigureAwait(false);
        return PlanMatcher.Candidates(plan, near, claimed);
    }

    /// <summary>
    /// Ticks a plan off, linked to the transaction it became if there is one, and creates the
    /// next occurrence of a repeating plan.
    /// </summary>
    /// <param name="id">The plan.</param>
    /// <param name="transactionId">The transaction it became, or null for a plan done without one.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<PlanResult> CompleteAsync(Guid id, Guid? transactionId, CancellationToken cancellationToken = default)
    {
        var plan = await plans.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return new PlanResult(null, "That plan no longer exists.");
        }

        if (plan.IsDone)
        {
            return new PlanResult(plan, null);
        }

        var next = plan.Complete(clock.UtcNow, transactionId);
        await plans.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);

        if (next is not null)
        {
            await plans.AddAsync(next, cancellationToken).ConfigureAwait(false);
        }

        return new PlanResult(plan, null);
    }

    /// <summary>
    /// Puts a completed plan back on the list, and withdraws the occurrence its completion
    /// created if that one has not been touched.
    /// </summary>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<PlanResult> ReopenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await plans.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return new PlanResult(null, "That plan no longer exists.");
        }

        if (plan.Repeat.NextAfter(plan.DueOn, plan.AnchorDay) is { } nextDue)
        {
            var open = await plans.ListOpenAsync(cancellationToken).ConfigureAwait(false);
            if (open.FirstOrDefault(p => p.SeriesId == plan.SeriesId && p.DueOn == nextDue) is { } spawned)
            {
                await plans.DeleteAsync(spawned.Id, cancellationToken).ConfigureAwait(false);
            }
        }

        plan.CompletedAt = null;
        plan.TransactionId = null;
        plan.UpdatedAt = clock.UtcNow;
        await plans.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);

        return new PlanResult(plan, null);
    }

    /// <summary>Removes a plan.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        plans.DeleteAsync(id, cancellationToken);

    /// <summary>
    /// Ticks off the open plan a newly recorded transaction fulfils, if exactly one is
    /// nearest, and links them.
    /// </summary>
    /// <param name="transaction">The transaction, with its parties loaded.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The plan ticked off, or null when none matched.</returns>
    public async Task<Plan?> CompleteMatchingAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var claimed = await plans.ClaimedTransactionIdsAsync(cancellationToken).ConfigureAwait(false);
        if (claimed.Contains(transaction.Id))
        {
            return null;
        }

        var open = await plans.ListOpenAsync(cancellationToken).ConfigureAwait(false);
        var match = open
            .Where(p => PlanMatcher.Matches(p, transaction))
            .OrderBy(p => Math.Abs(p.DueOn.DayNumber - transaction.OccurredOn.DayNumber))
            .FirstOrDefault();

        if (match is null)
        {
            return null;
        }

        await CompleteAsync(match.Id, transaction.Id, cancellationToken).ConfigureAwait(false);
        return match;
    }

    private static string? Validate(PlanDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            return "Give the plan a name.";
        }

        if (draft.AmountMinor is <= 0)
        {
            return "The amount must be more than zero, or left empty.";
        }

        if (draft.Direction == TransactionKind.SelfTransfer
            && draft.ContainerId is { } from && draft.DestinationContainerId == from)
        {
            return "Money cannot move to the container it is already in.";
        }

        return draft.Repeat.Interval < 1 && draft.Repeat.Frequency != RepeatFrequency.None
            ? "Repeat every 1 or more."
            : null;
    }

    private static void Apply(Plan plan, PlanDraft draft, DateTimeOffset now)
    {
        plan.Title = draft.Title.Trim();
        plan.Note = string.IsNullOrWhiteSpace(draft.Note) ? null : draft.Note.Trim();
        plan.AmountMinor = draft.AmountMinor;
        plan.Direction = draft.Direction;
        plan.ContainerId = draft.ContainerId;
        plan.CounterpartyName = draft.Direction == TransactionKind.SelfTransfer || string.IsNullOrWhiteSpace(draft.CounterpartyName)
            ? null
            : draft.CounterpartyName.Trim();
        plan.DestinationContainerId = draft.Direction == TransactionKind.SelfTransfer ? draft.DestinationContainerId : null;
        plan.DueOn = draft.DueOn;
        plan.DueTime = draft.DueTime;
        plan.Repeat = draft.Repeat.Frequency == RepeatFrequency.None ? RepeatRule.Never : draft.Repeat;
        plan.AnchorDay = draft.DueOn.Day;
        plan.UpdatedAt = now;
    }
}
