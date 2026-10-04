using EleFi.Domain.Parties;

namespace EleFi.Domain.Transactions;

/// <summary>
/// What sort of movement a transaction is. Always derived from its two parties, never
/// stored and never chosen.
/// </summary>
public enum TransactionKind
{
    /// <summary>Internal to External: money left the user.</summary>
    Debit = 0,

    /// <summary>External to Internal: money reached the user.</summary>
    Credit = 1,

    /// <summary>Internal to Internal: moved between the user's own containers.</summary>
    SelfTransfer = 2,
}

/// <summary>
/// Derives a transaction's kind from the two parties.
/// </summary>
/// <remarks>
/// Because the kind is computed rather than stored, a transaction whose kind disagrees
/// with its parties is not merely discouraged, it is unrepresentable. That is the whole
/// point of ADR-0002.
/// </remarks>
public static class TransactionKinds
{
    /// <summary>
    /// The kind implied by a source and destination party.
    /// </summary>
    /// <param name="source">The source party kind.</param>
    /// <param name="destination">The destination party kind.</param>
    /// <returns>The kind, or null for External to External, which is not the user's money (T1).</returns>
    public static TransactionKind? From(PartyKind source, PartyKind destination) => (source, destination) switch
    {
        (PartyKind.Container, PartyKind.External) => TransactionKind.Debit,
        (PartyKind.External, PartyKind.Container) => TransactionKind.Credit,
        (PartyKind.Container, PartyKind.Container) => TransactionKind.SelfTransfer,
        _ => null,
    };

    /// <summary>
    /// True when this kind counts as spending.
    /// </summary>
    /// <remarks>
    /// A Self Transfer is excluded, always (D1). Moving your own money is not spending,
    /// and this single exclusion is what stops a credit-card bill payment from
    /// double-counting every rupee already recorded when it was spent.
    /// </remarks>
    /// <param name="kind">The transaction kind.</param>
    public static bool IsSpend(this TransactionKind kind) => kind == TransactionKind.Debit;

    /// <summary>True when this kind counts as income. Self Transfers are excluded (D1).</summary>
    /// <param name="kind">The transaction kind.</param>
    public static bool IsIncome(this TransactionKind kind) => kind == TransactionKind.Credit;

    /// <summary>A short label for the UI.</summary>
    /// <param name="kind">The transaction kind.</param>
    public static string DisplayName(this TransactionKind kind) => kind switch
    {
        TransactionKind.Debit => "Debit",
        TransactionKind.Credit => "Credit",
        TransactionKind.SelfTransfer => "Self Transfer",
        _ => kind.ToString(),
    };
}
