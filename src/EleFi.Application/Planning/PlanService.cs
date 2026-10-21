using EleFi.Application.Abstractions;
using EleFi.Application.Transactions;
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
/// <param name="Repeat">How often it comes round, and on which days for a weekly plan.</param>
/// <param name="DueTime">The time it is due, if it matters.</param>
/// <param name="Note">Anything else.</param>
/// <param name="LastDayOfMonth">For a monthly plan: the last day of every month, whatever the due date's day.</param>
/// <param name="RepeatUntil">The last date an occurrence may fall on, if any.</param>
/// <param name="RepeatCount">How many occurrences in all, if limited.</param>
/// <param name="LabelIds">Labels to carry into the transaction it becomes.</param>
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
    string? Note = null,
    bool LastDayOfMonth = false,
    DateOnly? RepeatUntil = null,
    int? RepeatCount = null,
    IReadOnlyList<Guid>? LabelIds = null)
{
    /// <summary>The draft that would put a plan back exactly as it is now, for undo.</summary>
    /// <param name="plan">The plan as it stands.</param>
    public static PlanDraft From(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new PlanDraft(
            plan.Title,
            plan.DueOn,
            plan.AmountMinor,
            plan.Direction,
            plan.ContainerId,
            plan.CounterpartyName,
            plan.DestinationContainerId,
            plan.Repeat,
            plan.DueTime,
            plan.Note,
            plan.RepeatFrequency == RepeatFrequency.Monthly && plan.AnchorDay >= RepeatRule.LastDay,
            plan.RepeatUntil,
            plan.RepeatCount,
            [.. plan.Labels.Select(l => l.LabelId)]);
    }
}

/// <summary>The outcome of a plan change.</summary>
/// <param name="Plan">The plan, when it succeeded.</param>
/// <param name="Error">Why it was refused, in words the user can act on.</param>
public readonly record struct PlanResult(Plan? Plan, string? Error)
{
    /// <summary>True when the change was saved.</summary>
    public bool Succeeded => Error is null;
}

/// <summary>Money still planned: what is due to go out and come in, and how many plans.</summary>
/// <param name="OutMinor">Open plans paying out, in minor units.</param>
/// <param name="InMinor">Open plans to be received.</param>
/// <param name="Count">Open plans due, with an amount or not.</param>
public readonly record struct PlannedTotals(long OutMinor, long InMinor, int Count);

/// <summary>
/// The Planner: money the user expects to move, and ticking it off against the ledger.
/// </summary>
/// <remarks>
/// <para>
/// A plan never writes money. Ticking one off links it to a transaction that already exists
/// (recorded by hand, or by an SMS or a payment app), or the user records that transaction
/// through the ordinary capture form and the plan is linked to the result. So no plan can
/// put money in the ledger that nothing confirmed happened. The plan's labels go onto that
/// transaction when it has none of its own.
/// </para>
/// <para>
/// When an alert records a transaction that matches an open plan,
/// <see cref="CompleteMatchingAsync"/> ticks the plan off on its own: the SIP whose SMS came
/// needs nothing from the user, and the one whose SMS never came stays open on its due date,
/// with a reminder that morning.
/// </para>
/// <para>
/// Every change rebuilds the reminder schedule from scratch, so a plan deleted, done or
/// snoozed never leaves a stale alarm behind.
/// </para>
/// </remarks>
public sealed class PlanService(
    IPlanRepository plans,
    ITransactionRepository transactions,
    IClock clock,
    IAuditRepository audit,
    IReminderSchedule reminders)
{
    /// <summary>The table name the audit trail files plans under.</summary>
    public const string EntityType = "Plans";

    /// <summary>Plans still to do, soonest first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IReadOnlyList<Plan>> OpenAsync(CancellationToken cancellationToken = default) =>
        plans.ListOpenAsync(cancellationToken);

    /// <summary>Plans done or skipped, most recently first.</summary>
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
        await RescheduleAsync(cancellationToken).ConfigureAwait(false);
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
        await RescheduleAsync(cancellationToken).ConfigureAwait(false);
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

        if (transactionId is { } linked)
        {
            await CarryLabelsAsync(plan, linked, cancellationToken).ConfigureAwait(false);
        }

        await RescheduleAsync(cancellationToken).ConfigureAwait(false);
        return new PlanResult(plan, null);
    }

    /// <summary>
    /// Skips this occurrence without a transaction, and rolls a repeating plan on.
    /// </summary>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<PlanResult> SkipAsync(Guid id, CancellationToken cancellationToken = default)
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

        var next = plan.Skip(clock.UtcNow);
        await plans.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);

        if (next is not null)
        {
            await plans.AddAsync(next, cancellationToken).ConfigureAwait(false);
        }

        await RescheduleAsync(cancellationToken).ConfigureAwait(false);
        return new PlanResult(plan, null);
    }

    /// <summary>
    /// Puts a completed or skipped plan back on the list, and withdraws the occurrence its
    /// completion created if that one has not been touched.
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

        if (plan.NextDue() is { } nextDue)
        {
            var open = await plans.ListOpenAsync(cancellationToken).ConfigureAwait(false);
            if (open.FirstOrDefault(p => p.SeriesId == plan.SeriesId && p.DueOn == nextDue) is { } spawned)
            {
                await plans.DeleteAsync(spawned.Id, cancellationToken).ConfigureAwait(false);
            }
        }

        plan.CompletedAt = null;
        plan.TransactionId = null;
        plan.IsSkipped = false;
        plan.UpdatedAt = clock.UtcNow;
        await plans.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
        await RescheduleAsync(cancellationToken).ConfigureAwait(false);

        return new PlanResult(plan, null);
    }

    /// <summary>Moves a plan's reminder to tomorrow. Its due date stays as it was.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task SnoozeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await plans.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (plan is null || plan.IsDone)
        {
            return;
        }

        plan.RemindOn = clock.Today.AddDays(1);
        await plans.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
        await RescheduleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes a plan. Soft, like every delete.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await plans.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        await RescheduleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Brings a deleted plan back, for undo.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await plans.RestoreAsync(id, cancellationToken).ConfigureAwait(false);
        await RescheduleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// What is still planned up to the end of this month: open plans due by then, overdue
    /// ones included, with what they pay out and bring in.
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<PlannedTotals> StillPlannedAsync(CancellationToken cancellationToken = default)
    {
        var today = clock.Today;
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var due = (await plans.ListOpenAsync(cancellationToken).ConfigureAwait(false))
            .Where(p => p.DueOn <= monthEnd)
            .ToList();

        return new PlannedTotals(
            due.Where(p => p.Direction == TransactionKind.Debit).Sum(p => p.AmountMinor ?? 0),
            due.Where(p => p.Direction == TransactionKind.Credit).Sum(p => p.AmountMinor ?? 0),
            due.Count);
    }

    /// <summary>Everything that has happened to a plan, newest first, as a person reads it.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="names">Names for the containers the trail may mention.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<HistoryEntry>> HistoryAsync(
        Guid id,
        IReadOnlyDictionary<Guid, string> names,
        CancellationToken cancellationToken = default)
    {
        var events = await audit.TimelineAsync(EntityType, id, cancellationToken).ConfigureAwait(false);
        return TransactionHistory.Describe(events, names, Domain.Money.Currency.Inr);
    }

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

    /// <summary>Rebuilds every reminder from the open plans. Also run at start-up.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task RescheduleAsync(CancellationToken cancellationToken = default)
    {
        var open = await plans.ListOpenAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;
        reminders.Replace(PlanReminders.Schedule(open, now, TimeZoneInfo.Local.GetUtcOffset(now)));
    }

    // Onto a transaction with no labels of its own: the user's labels always win, a Self
    // Transfer carries none (T6), and an itemised one keeps its labels on its items (IT4).
    private async Task CarryLabelsAsync(Plan plan, Guid transactionId, CancellationToken cancellationToken)
    {
        if (plan.Labels.Count == 0)
        {
            return;
        }

        var transaction = await transactions.FindAsync(transactionId, cancellationToken).ConfigureAwait(false);
        if (transaction is null || transaction.Labels.Count > 0 || transaction.IsItemised
            || transaction.SourceParty is null || transaction.DestinationParty is null
            || transaction.Kind == TransactionKind.SelfTransfer)
        {
            return;
        }

        foreach (var label in plan.Labels.Select(l => l.LabelId).Distinct())
        {
            transaction.Labels.Add(new TransactionLabel { TransactionId = transaction.Id, LabelId = label });
        }

        await transactions.UpdateAsync(transaction, cancellationToken).ConfigureAwait(false);
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

        if (draft.Repeat.Frequency != RepeatFrequency.None)
        {
            if (draft.Repeat.Interval < 1)
            {
                return "Repeat every 1 or more.";
            }

            if (draft.RepeatUntil is { } until && until < draft.DueOn)
            {
                return "The repeat has to end on or after the first due date.";
            }

            if (draft.RepeatCount is < 1)
            {
                return "Repeat at least once, or leave the count empty.";
            }
        }

        return null;
    }

    private static void Apply(Plan plan, PlanDraft draft, DateTimeOffset now)
    {
        var repeats = draft.Repeat.Frequency != RepeatFrequency.None;

        plan.Title = draft.Title.Trim();
        plan.Note = string.IsNullOrWhiteSpace(draft.Note) ? null : draft.Note.Trim();
        plan.AmountMinor = draft.AmountMinor;
        plan.Direction = draft.Direction;
        plan.ContainerId = draft.ContainerId;
        plan.CounterpartyName = draft.Direction == TransactionKind.SelfTransfer || string.IsNullOrWhiteSpace(draft.CounterpartyName)
            ? null
            : draft.CounterpartyName.Trim();
        plan.DestinationContainerId = draft.Direction == TransactionKind.SelfTransfer ? draft.DestinationContainerId : null;

        // A new due date clears a snooze: the reminder follows the date again.
        if (plan.DueOn != draft.DueOn)
        {
            plan.RemindOn = null;
        }

        plan.DueOn = draft.DueOn;
        plan.DueTime = draft.DueTime;
        plan.Repeat = repeats ? draft.Repeat : RepeatRule.Never;
        plan.AnchorDay = draft.LastDayOfMonth && draft.Repeat.Frequency == RepeatFrequency.Monthly
            ? RepeatRule.LastDay
            : draft.DueOn.Day;
        plan.RepeatUntil = repeats ? draft.RepeatUntil : null;
        plan.RepeatCount = repeats ? draft.RepeatCount : null;
        plan.UpdatedAt = now;

        // Labels: none on a transfer, as on a transaction (T6).
        var labels = draft.Direction == TransactionKind.SelfTransfer ? [] : (draft.LabelIds ?? []).Distinct().ToList();
        plan.Labels.RemoveAll(l => !labels.Contains(l.LabelId));
        foreach (var label in labels.Where(id => plan.Labels.All(l => l.LabelId != id)))
        {
            plan.Labels.Add(new PlanLabel { PlanId = plan.Id, LabelId = label });
        }
    }
}
