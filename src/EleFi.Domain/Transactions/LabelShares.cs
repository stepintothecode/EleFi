using EleFi.Domain.Labels;

namespace EleFi.Domain.Transactions;

/// <summary>One label's share of a transaction's money, or the unlabelled part of it.</summary>
/// <param name="Label">The label, or null for money that carries none.</param>
/// <param name="AmountMinor">How much of the transaction counts under it.</param>
public readonly record struct LabelShare(Label? Label, long AmountMinor);

/// <summary>
/// How a transaction's money divides among its labels, for the spend breakdown.
/// </summary>
/// <remarks>
/// <para>
/// Without items, the whole amount counts under each of its labels, and the buckets overlap
/// by design (ADR-0013). With items, each item's amount counts under that item's labels
/// instead, so a ₹500 shop split into ₹200 Food and ₹300 Groceries counts ₹200 and ₹300, not
/// ₹500 twice (ADR-0017). An item with two labels still counts under both.
/// </para>
/// <para>
/// Money with no label is a share too, with a null label, so the breakdown's Unlabelled bucket
/// sees it. The total is never the sum of these: it is computed once per transaction.
/// </para>
/// </remarks>
public static class LabelShares
{
    /// <summary>The shares of one transaction, labels and items loaded.</summary>
    /// <param name="transaction">The transaction.</param>
    public static IEnumerable<LabelShare> Of(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return transaction.IsItemised
            ? transaction.Items.SelectMany(i => Shares(i.Labels.Select(l => l.Label), i.AmountMinor))
            : Shares(transaction.Labels.Select(l => l.Label), transaction.SourceAmountMinor);
    }

    private static IEnumerable<LabelShare> Shares(IEnumerable<Label?> labels, long amountMinor)
    {
        var known = labels.OfType<Label>().ToList();
        return known.Count == 0
            ? [new LabelShare(null, amountMinor)]
            : known.Select(l => new LabelShare(l, amountMinor));
    }
}
