using EleFi.Domain.Money;
using EleFi.Domain.Parties;
using EleFi.Domain.Primitives;

namespace EleFi.Domain.Transactions;

/// <summary>
/// One movement of money, recorded once.
/// </summary>
/// <remarks>
/// <para>
/// Each end carries its own amount and currency, because a cross-currency movement
/// genuinely has two amounts: 8,300 leaves and 100 arrives, and both are facts the bank
/// asserted. When the currencies match, T4 constrains the two amounts to be equal, so a
/// same-currency transaction behaves exactly as a one-amount model would.
/// </para>
/// <para>
/// The kind is not a field here. It is derived from the two parties every time it is
/// asked for, which makes a transaction whose kind disagrees with its data
/// unrepresentable rather than merely discouraged.
/// </para>
/// </remarks>
public class Transaction : Entity
{
    /// <summary>The party money moved from.</summary>
    public Guid SourcePartyId { get; set; }

    /// <summary>Navigation to the source party.</summary>
    public Party? SourceParty { get; set; }

    /// <summary>What left the source, in that currency's minor units. Always positive (T3).</summary>
    public long SourceAmountMinor { get; set; }

    /// <summary>The source currency code.</summary>
    public string SourceCurrencyCode { get; set; } = Currency.Inr.Code;

    /// <summary>The party money moved to.</summary>
    public Guid DestinationPartyId { get; set; }

    /// <summary>Navigation to the destination party.</summary>
    public Party? DestinationParty { get; set; }

    /// <summary>What arrived at the destination, in minor units. Always positive (T3).</summary>
    public long DestinationAmountMinor { get; set; }

    /// <summary>The destination currency code.</summary>
    public string DestinationCurrencyCode { get; set; } = Currency.Inr.Code;

    /// <summary>
    /// The calendar day the movement happened.
    /// </summary>
    /// <remarks>
    /// A date, not an instant. Storing a timestamp would introduce a timezone bug the day
    /// the user crosses one, and the transaction would appear to move.
    /// </remarks>
    public DateOnly OccurredOn { get; set; }

    /// <summary>
    /// The time of day it happened, or null when only the date is known.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A separate field rather than folding it into <see cref="OccurredOn"/>, so the date
    /// stays a calendar day. Combining them would make the transaction an instant, and an
    /// instant shifts across timezones: a 1am purchase in Chennai would move to the previous
    /// day the moment the phone lands somewhere west.
    /// </para>
    /// <para>
    /// Nullable because a back-dated entry made from memory has a date and no honest time,
    /// and inventing midnight would be a fact the user never supplied.
    /// </para>
    /// </remarks>
    public TimeOnly? OccurredAtTime { get; set; }

    /// <summary>Free text the user typed.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// The categories this was for. Any number, including none.
    /// </summary>
    /// <remarks>
    /// Many rather than one, and optional (ADR-0013). Per-label totals therefore overlap and
    /// do not add up to total spend: a shop labelled both Food and Household is counted in
    /// full under each.
    /// </remarks>
    public List<TransactionLabel> Labels { get; } = [];

    /// <summary>The platform bought through: Zomato, Uber.</summary>
    public Guid? MarketplaceAppId { get; set; }

    /// <summary>Navigation to the marketplace app.</summary>
    public Apps.App? MarketplaceApp { get; set; }

    /// <summary>The rail the money moved along: GPay, Net Banking, Cash.</summary>
    public Guid? PaymentAppId { get; set; }

    /// <summary>Navigation to the payment app.</summary>
    public Apps.App? PaymentApp { get; set; }

    /// <summary>The goal this contributes to, in Tracks Contributions mode (T8).</summary>
    public Guid? GoalId { get; set; }

    /// <summary>Set when values were guessed and the user has not confirmed them.</summary>
    /// <remarks>A nudge, never a restriction. The transaction counts toward every balance regardless.</remarks>
    public bool NeedsReview { get; set; }

    /// <summary>How this transaction was recorded.</summary>
    public CaptureSource CaptureSource { get; set; } = CaptureSource.App;

    /// <summary>The source currency, as a value type.</summary>
    public Currency SourceCurrency => Currency.Of(SourceCurrencyCode);

    /// <summary>The destination currency, as a value type.</summary>
    public Currency DestinationCurrency => Currency.Of(DestinationCurrencyCode);

    /// <summary>What left the source.</summary>
    public Money.Money SourceAmount => Money.Money.FromMinor(SourceAmountMinor, SourceCurrency);

    /// <summary>What arrived at the destination.</summary>
    public Money.Money DestinationAmount => Money.Money.FromMinor(DestinationAmountMinor, DestinationCurrency);

    /// <summary>True when the two ends are in different currencies.</summary>
    public bool IsCrossCurrency =>
        !string.Equals(SourceCurrencyCode, DestinationCurrencyCode, StringComparison.Ordinal);

    /// <summary>
    /// The kind, derived from the two parties.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The parties are not loaded, or both are external, which T1 forbids.
    /// </exception>
    public TransactionKind Kind
    {
        get
        {
            if (SourceParty is null || DestinationParty is null)
            {
                throw new InvalidOperationException(
                    "Both parties must be loaded before the kind can be derived. "
                    + "Repositories include them; a projection that drops them is a bug.");
            }

            return TransactionKinds.From(SourceParty.Kind, DestinationParty.Kind)
                ?? throw new InvalidOperationException(
                    "External to External is not a transaction: it is not the user's money (T1).");
        }
    }
}

/// <summary>Joins a transaction to one of its labels.</summary>
public class TransactionLabel
{
    /// <summary>The transaction.</summary>
    public Guid TransactionId { get; set; }

    /// <summary>The label.</summary>
    public Guid LabelId { get; set; }

    /// <summary>Navigation to the label.</summary>
    public Labels.Label? Label { get; set; }
}
