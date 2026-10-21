using EleFi.Application.Abstractions;
using EleFi.Domain.Filters;
using EleFi.Domain.Money;
using EleFi.Domain.Receipts;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Receipts;

/// <summary>
/// Reads a scanned receipt and finds the transactions it could belong to.
/// </summary>
/// <remarks>
/// Candidates are the Debits and Credits within three days of the receipt's date, or of today
/// when it shows none, through the same query the list uses. Ordered by how well each agrees,
/// so "Pick another" shows the likeliest first.
/// </remarks>
public sealed class ReceiptMatcher(ITransactionRepository transactions, IClock clock)
{
    private const int Window = 3;

    /// <summary>What the receipt says, or null when there is no amount on it to go by.</summary>
    /// <param name="scan">The scanned receipt.</param>
    public ReceiptReading? Read(ScannedReceipt scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        return ReceiptReader.Read(scan.Lines, Currency.Inr, clock.Today);
    }

    /// <summary>Transactions around the receipt's date, likeliest first.</summary>
    /// <param name="reading">What the receipt says.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<Transaction>> CandidatesAsync(ReceiptReading reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var day = reading.Facts.OccurredOn ?? clock.Today;
        var filter = new TransactionFilter
        {
            From = day.AddDays(-Window),
            To = day.AddDays(Window),
            Kinds = [TransactionKind.Debit, TransactionKind.Credit],
        };

        var nearby = await transactions.QueryAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return [.. nearby.OrderByDescending(t => ReceiptMatch.Score(reading, t))];
    }

    /// <summary>The transaction the receipt belongs to, or null when none is close enough.</summary>
    /// <param name="reading">What the receipt says.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<Transaction?> BestAsync(ReceiptReading reading, CancellationToken cancellationToken = default) =>
        ReceiptMatch.Best(reading, await CandidatesAsync(reading, cancellationToken).ConfigureAwait(false));
}
