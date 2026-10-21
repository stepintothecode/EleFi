using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Receipts;

/// <summary>What a receipt and its transaction disagree on.</summary>
[Flags]
public enum Discrepancy
{
    /// <summary>They agree on everything the receipt shows.</summary>
    None = 0,

    /// <summary>The receipt shows a different amount.</summary>
    Amount = 1,

    /// <summary>The receipt names a different payee.</summary>
    Payee = 2,

    /// <summary>The receipt was paid from a different account.</summary>
    Account = 4,
}

/// <summary>
/// Compares a receipt with a transaction (RC4).
/// </summary>
/// <remarks>
/// Only what the receipt actually shows is compared: a shop receipt with no account on it can
/// never disagree about the account. Derived every time rather than stored, so correcting the
/// transaction clears a discrepancy by itself.
/// </remarks>
public static class ReceiptCheck
{
    /// <summary>What a receipt and a transaction disagree on.</summary>
    /// <param name="receipt">What the receipt shows.</param>
    /// <param name="transaction">The transaction, with its parties loaded.</param>
    /// <param name="containerLast4">The last four digits of the transaction's own container, when known.</param>
    /// <param name="otherNames">Other names the counterparty goes by: its aliases.</param>
    public static Discrepancy Compare(ReceiptFacts receipt, Transaction transaction, string? containerLast4, IEnumerable<string>? otherNames = null)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(transaction);

        var found = Discrepancy.None;

        if (receipt.AmountMinor is { } amount && amount != transaction.SourceAmountMinor)
        {
            found |= Discrepancy.Amount;
        }

        if (receipt.Payee is { Length: > 0 } payee && Counterparty(transaction) is { } name
            && !new[] { name }.Concat(otherNames ?? []).Any(n => PayeeNames.Same(payee, n)))
        {
            found |= Discrepancy.Payee;
        }

        if (receipt.AccountLast4 is { } last4 && containerLast4 is { Length: 4 } && last4 != containerLast4)
        {
            found |= Discrepancy.Account;
        }

        return found;
    }

    /// <summary>The name of whoever was on the other side, or null for a Self Transfer.</summary>
    /// <param name="transaction">The transaction, with its parties loaded.</param>
    public static string? Counterparty(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return transaction.DestinationParty is { Kind: PartyKind.External } to ? to.Name
            : transaction.SourceParty is { Kind: PartyKind.External } from ? from.Name
            : null;
    }
}
