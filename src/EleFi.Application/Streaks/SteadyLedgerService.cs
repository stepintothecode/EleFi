using EleFi.Application.Abstractions;
using EleFi.Domain.Streaks;

namespace EleFi.Application.Streaks;

/// <summary>
/// The Steady ledger streak (S42), worked out from the ledger and the plans as they are now.
/// </summary>
/// <remarks>
/// Nothing is stored: the streak is derived on every read, like a balance, so tidying an old
/// week mends it and there is no counter to drift out of step with the ledger.
/// </remarks>
/// <param name="transactions">The ledger.</param>
/// <param name="plans">The plans.</param>
/// <param name="clock">Today.</param>
public sealed class SteadyLedgerService(ITransactionRepository transactions, IPlanRepository plans, IClock clock)
{
    /// <summary>Where the streak stands today.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<SteadyStreak> CurrentAsync(CancellationToken cancellationToken = default)
    {
        var facts = await transactions.ReviewFactsAsync(cancellationToken).ConfigureAwait(false);
        var open = await plans.ListOpenAsync(cancellationToken).ConfigureAwait(false);

        // The day the user started is the local day of their first record.
        DateOnly? started = facts.FirstRecordedAt is { } first
            ? DateOnly.FromDateTime(first.ToOffset(TimeZoneInfo.Local.GetUtcOffset(first)).DateTime)
            : null;

        return SteadyLedger.Compute(started, facts.NeedsReviewOn, open.Select(p => p.DueOn), clock.Today);
    }
}
