using EleFi.Domain.Money;
using EleFi.Domain.Receipts;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Receipts;

/// <summary>How a discrepancy reads: a short tag for a list, and sentences for the details.</summary>
/// <remarks>
/// One wording for the receipt screen, the transaction's receipts and what Ele says, so a
/// disagreement is never described two ways.
/// </remarks>
public static class DiscrepancyText
{
    /// <summary>A few words for a list: "Amount differs", "2 differences", "Matches".</summary>
    /// <param name="found">What differs.</param>
    public static string Tag(Discrepancy found)
    {
        var parts = Parts(found).ToList();
        return parts.Count switch
        {
            0 => "Matches",
            1 => $"{parts[0]} differs",
            _ => $"{parts.Count} differences",
        };
    }

    /// <summary>
    /// What Ele says, short enough for her bubble: the one difference in full, or which ones
    /// when there are several.
    /// </summary>
    /// <param name="found">What differs.</param>
    /// <param name="receipt">What the receipt says.</param>
    /// <param name="transaction">The transaction, with parties loaded.</param>
    public static string Summary(Discrepancy found, ReceiptFacts receipt, Transaction transaction)
    {
        var parts = Parts(found).ToList();
        if (parts.Count == 1)
        {
            return Details(found, receipt, transaction)[0];
        }

        var lower = parts.Select((p, i) => i == 0 ? p : p.ToLowerInvariant()).ToList();
        var named = string.Join(", ", lower.Take(lower.Count - 1)) + " and " + lower[^1];
        return $"{named} differ from this one.";
    }

    /// <summary>One sentence per difference, saying what each side has.</summary>
    /// <param name="found">What differs.</param>
    /// <param name="receipt">What the receipt says.</param>
    /// <param name="transaction">The transaction, with parties loaded.</param>
    public static IReadOnlyList<string> Details(Discrepancy found, ReceiptFacts receipt, Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(transaction);

        var lines = new List<string>();
        if (found.HasFlag(Discrepancy.Amount) && receipt.AmountMinor is { } amount)
        {
            lines.Add($"Amount: {Show(amount, transaction)} on the receipt, {Show(transaction.SourceAmountMinor, transaction)} here.");
        }

        if (found.HasFlag(Discrepancy.Payee))
        {
            lines.Add($"Payee: {receipt.Payee} on the receipt, {ReceiptCheck.Counterparty(transaction)} here.");
        }

        if (found.HasFlag(Discrepancy.Account))
        {
            lines.Add($"Account: ending {receipt.AccountLast4} on the receipt, a different one here.");
        }

        return lines;
    }

    private static IEnumerable<string> Parts(Discrepancy found)
    {
        if (found.HasFlag(Discrepancy.Amount))
        {
            yield return "Amount";
        }

        if (found.HasFlag(Discrepancy.Payee))
        {
            yield return "Payee";
        }

        if (found.HasFlag(Discrepancy.Account))
        {
            yield return "Account";
        }
    }

    private static string Show(long minor, Transaction transaction) =>
        MoneyText.ToDisplayString(Money.FromMinor(minor, transaction.SourceCurrency));
}
