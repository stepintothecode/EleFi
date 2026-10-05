using EleFi.Domain.Transactions;

namespace EleFi.Domain.Planning;

/// <summary>
/// Finds the transaction that already records a plan.
/// </summary>
/// <remarks>
/// <para>
/// A plan is ticked off by linking it to the transaction it became, so the same money is
/// never recorded twice: once by the bank's SMS and again by ticking the plan. The match is
/// strict on the amount (to the paisa) and the container, and loose on the date, because a
/// SIP debit can land a few days either side of its date.
/// </para>
/// <para>
/// A plan for a transfer into a mutual fund often reaches the ledger as a Debit, because
/// the bank's SMS names the fund house rather than the user's fund container. So a transfer
/// plan accepts a Debit from its Source container as well.
/// </para>
/// </remarks>
public static class PlanMatcher
{
    /// <summary>How far from the due date a transaction can be and still be this plan's.</summary>
    public static int WindowDays => 4;

    /// <summary>True when the transaction could be the one this plan expected.</summary>
    /// <param name="plan">An open plan with an amount.</param>
    /// <param name="transaction">A transaction with its parties loaded.</param>
    public static bool Matches(Plan plan, Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(transaction);

        if (plan.IsDone || plan.AmountMinor is not { } amount
            || transaction.SourceParty is null || transaction.DestinationParty is null)
        {
            return false;
        }

        if (transaction.SourceAmountMinor != amount
            || Math.Abs(transaction.OccurredOn.DayNumber - plan.DueOn.DayNumber) > WindowDays)
        {
            return false;
        }

        var kind = transaction.Kind;
        var sameMovement = kind == plan.Direction
            || (plan.Direction == TransactionKind.SelfTransfer && kind == TransactionKind.Debit);

        if (!sameMovement)
        {
            return false;
        }

        // The plan's container, when it names one, must be at the end the plan expects.
        if (plan.ContainerId is { } container)
        {
            var end = plan.Direction == TransactionKind.Credit
                ? transaction.DestinationParty.ContainerId
                : transaction.SourceParty.ContainerId;

            if (end != container)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The transactions that could record this plan, nearest to its due date first.</summary>
    /// <param name="plan">An open plan.</param>
    /// <param name="transactions">Transactions near its due date, with parties loaded.</param>
    /// <param name="claimed">Transactions already linked to some plan, which are never offered twice.</param>
    public static IReadOnlyList<Transaction> Candidates(
        Plan plan,
        IEnumerable<Transaction> transactions,
        IReadOnlySet<Guid> claimed)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(claimed);

        return transactions
            .Where(t => !claimed.Contains(t.Id) && Matches(plan, t))
            .OrderBy(t => Math.Abs(t.OccurredOn.DayNumber - plan.DueOn.DayNumber))
            .ToList();
    }
}
