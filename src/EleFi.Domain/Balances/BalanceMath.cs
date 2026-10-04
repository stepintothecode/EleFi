using EleFi.Domain.Containers;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Balances;

/// <summary>A container and what it currently holds.</summary>
/// <param name="ContainerId">The container.</param>
/// <param name="Name">The container's name, for display.</param>
/// <param name="Kind">The container's kind, which decides liability and liquidity.</param>
/// <param name="Balance">The derived balance. Signed: a card's is what is owed.</param>
public readonly record struct ContainerBalance(
    Guid ContainerId,
    string Name,
    ContainerKind Kind,
    Money.Money Balance)
{
    /// <summary>True when this balance is money owed rather than money held.</summary>
    public bool IsLiability => Kind.IsLiability();

    /// <summary>
    /// The amount owed on a liability. Negative when the card is in credit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A liability's ledger balance is negative while money is owed, so owing is the
    /// negation of the balance. This used to be <c>Abs()</c>, which reported a card you had
    /// overpaid as if you owed that amount: paying 2,000 onto a card with nothing on it
    /// showed "2,000 owed" instead of "2,000 in credit", and the number moved the wrong way
    /// as you repaid.
    /// </para>
    /// <para>
    /// The sign is kept rather than hidden so the UI can say which of the two it is.
    /// </para>
    /// </remarks>
    public Money.Money AmountOwed =>
        IsLiability
            ? Money.Money.SignedMinor(-Balance.Minor, Balance.Currency)
            : Money.Money.Zero(Balance.Currency);

    /// <summary>True when a liability has been overpaid and the issuer owes the user.</summary>
    public bool IsInCredit => IsLiability && Balance.Minor > 0;
}

/// <summary>
/// Net worth, always presented with its three components.
/// </summary>
/// <remarks>
/// The total alone hides the situation that actually matters: someone with most of their
/// wealth in a PPF and a large card bill is not in the position a single number suggests.
/// </remarks>
/// <param name="Liquid">Assets spendable this week.</param>
/// <param name="Locked">Assets that are real wealth but not spendable on demand.</param>
/// <param name="Owed">Liabilities, as a positive amount.</param>
public readonly record struct NetWorth(Money.Money Liquid, Money.Money Locked, Money.Money Owed)
{
    /// <summary>Total assets minus total liabilities.</summary>
    public Money.Money Total => Liquid + Locked - Owed;
}

/// <summary>
/// Derives balances from opening balances and transactions. Pure, and the reference
/// implementation the property tests check the database against.
/// </summary>
/// <remarks>
/// <b>No balance is ever stored (D2).</b> Back-dated entries, edits, and deletes are
/// routine in a manual tracker, and each one would need a stored balance to rewind and
/// replay correctly. Any bug in that leaves a silently wrong number, which the user does
/// not notice until they reconcile against a real bank statement and stop trusting the app.
/// </remarks>
public static class BalanceMath
{
    /// <summary>
    /// Replays a container's transactions over its opening balance.
    /// </summary>
    /// <remarks>
    /// Money arriving at the container's party adds; money leaving subtracts. For a credit
    /// card that reads the way it should: spending on the card is a Debit with the card as
    /// source, so it subtracts and the balance goes further negative, meaning more owed.
    /// </remarks>
    /// <param name="container">The container.</param>
    /// <param name="partyId">The party row that represents this container.</param>
    /// <param name="transactions">Every live transaction touching it, in any order.</param>
    public static Money.Money Balance(
        Container container,
        Guid partyId,
        IEnumerable<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(transactions);

        var currency = container.Currency;
        var minor = container.OpeningBalanceMinor;

        foreach (var transaction in transactions)
        {
            if (transaction.IsDeleted)
            {
                continue;
            }

            if (transaction.DestinationPartyId == partyId)
            {
                minor += transaction.DestinationAmountMinor;
            }

            if (transaction.SourcePartyId == partyId)
            {
                minor -= transaction.SourceAmountMinor;
            }
        }

        return Money.Money.SignedMinor(minor, currency);
    }

    /// <summary>
    /// Splits balances into Liquid, Locked, and Owed.
    /// </summary>
    /// <remarks>
    /// Every balance must already be in the home currency: this does no conversion, on
    /// purpose, so an unconverted foreign amount fails loudly rather than being silently
    /// added to a rupee total.
    /// </remarks>
    /// <param name="balances">Container balances, all in the home currency.</param>
    /// <param name="homeCurrency">The home currency.</param>
    public static NetWorth Summarise(IEnumerable<ContainerBalance> balances, Currency homeCurrency)
    {
        ArgumentNullException.ThrowIfNull(balances);

        var liquid = Money.Money.SignedMinor(0, homeCurrency);
        var locked = Money.Money.SignedMinor(0, homeCurrency);
        var owed = Money.Money.SignedMinor(0, homeCurrency);

        foreach (var balance in balances)
        {
            if (balance.IsLiability)
            {
                // Signed, not absolute. An overpaid card reduces what is owed rather than
                // adding to it, and net worth is Liquid + Locked - Owed either way.
                owed += balance.AmountOwed;
            }
            else if (balance.Kind.IsLiquid())
            {
                liquid += balance.Balance;
            }
            else
            {
                locked += balance.Balance;
            }
        }

        return new NetWorth(liquid, locked, owed);
    }
}
