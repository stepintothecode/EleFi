using EleFi.Domain.Transactions;

namespace EleFi.Domain.Receipts;

/// <summary>
/// Finds the transaction a receipt belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Each candidate scores on what agrees: the amount (4), the payee (3), the same day (2) or
/// the day either side (1), and the time within half an hour (1). A candidate more than three
/// days from the receipt's date is never the one. Five or more is a match, so the amount on
/// the right day matches, and so does the payee on the right day even when the amount was
/// mistyped, which is exactly the receipt that should arrive with a discrepancy.
/// </para>
/// <para>
/// Below five is no match, and the receipt is recorded as a new transaction for review rather
/// than pinned to a guess.
/// </para>
/// </remarks>
public static class ReceiptMatch
{
    /// <summary>The lowest score that counts as the same payment.</summary>
    public const int Threshold = 5;

    /// <summary>The candidate the receipt belongs to, or null when none is close enough.</summary>
    /// <param name="reading">What was read from the receipt.</param>
    /// <param name="candidates">Transactions around its date, with parties loaded.</param>
    public static Transaction? Best(ReceiptReading reading, IEnumerable<Transaction> candidates)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Select(t => (Transaction: t, Score: Score(reading, t)))
            .Where(c => c.Score >= Threshold)
            .OrderByDescending(c => c.Score)
            .ThenBy(c => DaysApart(reading, c.Transaction))
            .Select(c => c.Transaction)
            .FirstOrDefault();
    }

    /// <summary>How well a transaction agrees with a receipt; below zero when it cannot be the one.</summary>
    /// <param name="reading">What was read.</param>
    /// <param name="transaction">A candidate, with parties loaded.</param>
    public static int Score(ReceiptReading reading, Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(transaction);

        var days = DaysApart(reading, transaction);
        if (days > 3)
        {
            return -1;
        }

        var score = 0;
        if (reading.Facts.AmountMinor == transaction.SourceAmountMinor)
        {
            score += 4;
        }

        if (reading.Facts.Payee is { } payee && ReceiptCheck.Counterparty(transaction) is { } name && PayeeNames.Same(payee, name))
        {
            score += 3;
        }

        if (reading.Facts.OccurredOn is not null)
        {
            score += days switch { 0 => 2, 1 => 1, _ => 0 };
        }

        if (reading.OccurredAtTime is { } at && transaction.OccurredAtTime is { } then && days == 0
            && Math.Abs(at.ToTimeSpan().TotalMinutes - then.ToTimeSpan().TotalMinutes) <= 30)
        {
            score += 1;
        }

        return score;
    }

    // A receipt with no date is as close to every candidate as can be told.
    private static int DaysApart(ReceiptReading reading, Transaction transaction) =>
        reading.Facts.OccurredOn is { } on ? Math.Abs(on.DayNumber - transaction.OccurredOn.DayNumber) : 0;
}
