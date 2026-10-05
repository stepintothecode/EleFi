using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Alerts;

/// <summary>Where a suggestion is in its short life.</summary>
public enum SuggestionState
{
    /// <summary>Waiting for the user to accept or dismiss it.</summary>
    Pending = 0,

    /// <summary>The user accepted it and a real transaction now exists.</summary>
    Confirmed = 1,

    /// <summary>The user rejected it. The row is destroyed (SM5).</summary>
    Dismissed = 2,

    /// <summary>Nobody acted before it expired. The row is destroyed (SM5).</summary>
    Expired = 3,
}

/// <summary>
/// What a Parse Rule read from a bank's SMS, a Payment App's notification, or both describing
/// the same payment, and the link to the transaction it recorded.
/// </summary>
/// <remarks>
/// <para>
/// <b>Since ADR-0015 this is bookkeeping, not something the user sees.</b> An alert is
/// recorded straight away as a transaction flagged Needs Review, and this row is created
/// already <see cref="SuggestionState.Confirmed"/>, pointing at it. It exists so the second
/// alert for a payment completes that transaction instead of recording another, and so a
/// repeated alert is recognised (SM6). It expires after a week.
/// </para>
/// <para>
/// It was once a proposal awaiting confirmation (SM2, ADR-0010). The pending state and the
/// confirming code paths are gone; the states remain so old rows still read.
/// </para>
/// <para>
/// Note what is <b>not</b> here: no body column and no sender column. The message text is
/// never written to disk (SM1), and the sender is folded into
/// <see cref="Fingerprint"/> rather than stored beside it.
/// </para>
/// </remarks>
public class CaptureSuggestion
{
    /// <summary>The client-generated UUIDv7 primary key.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The rule that produced this suggestion.</summary>
    public Guid ParseRuleId { get; set; }

    /// <summary>
    /// A one-way hash of sender, timestamp, amount and last four, used only to suppress
    /// duplicates (SM6) so a re-delivered alert produces one suggestion rather than two.
    /// </summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>The amount the rule read, in minor units.</summary>
    public long AmountMinor { get; set; }

    /// <summary>The currency, defaulting to the home currency when the message did not say.</summary>
    public string CurrencyCode { get; set; } = Currency.Inr.Code;

    /// <summary>Whether the message described money leaving or arriving.</summary>
    public TransactionKind Direction { get; set; }

    /// <summary>
    /// The matched container, or null when the last four digits matched nothing or matched
    /// more than one thing (SM11). Never inferred from the counterparty or the amount.
    /// </summary>
    public Guid? ContainerId { get; set; }

    /// <summary>The merchant or payer text the rule captured, unresolved.</summary>
    public string? CounterpartyText { get; set; }

    /// <summary>The date the rule read, or null when the message did not carry one.</summary>
    public DateOnly? OccurredOn { get; set; }

    /// <summary>Where this suggestion is in its life.</summary>
    public SuggestionState State { get; set; } = SuggestionState.Pending;

    /// <summary>The transaction created on confirmation. The proof that stops a second one (SM7).</summary>
    public Guid? TransactionId { get; set; }

    /// <summary>When it was created, UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When it stops being offered and is destroyed.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Which channels have described this payment: the bank's SMS, the Payment App's
    /// notification, or both.
    /// </summary>
    public AlertEvidence Evidence { get; set; } = AlertEvidence.Sms;

    /// <summary>The Payment App the money moved through, when an app reported it: "GPay".</summary>
    public string? PaymentAppName { get; set; }

    /// <summary>A note the payer attached in the Payment App, if there was one.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// The fingerprint of the second alert merged into this one, so a re-posted
    /// notification is recognised as a duplicate rather than offered again (SM6).
    /// </summary>
    public string? CorroboratingFingerprint { get; set; }

    /// <summary>
    /// The time of day the payment happened, when a Payment App reported it. Null for an SMS
    /// alone, whose timestamp is when the bank noticed rather than when the money moved.
    /// </summary>
    public TimeOnly? OccurredAtTime { get; set; }

    /// <summary>The amount, as a value type.</summary>
    public Money.Money Amount => Money.Money.FromMinor(AmountMinor, Currency.Of(CurrencyCode));

    /// <summary>True when this is still waiting for the user.</summary>
    public bool IsPending => State == SuggestionState.Pending;

    /// <summary>
    /// A new pending suggestion from one parsed alert.
    /// </summary>
    /// <param name="alert">What the rule extracted.</param>
    /// <param name="fingerprint">The alert's duplicate-suppression fingerprint.</param>
    /// <param name="containerId">The container matched by last four digits, if exactly one was (SM11).</param>
    /// <param name="today">The date to assume when the alert carried none.</param>
    /// <param name="now">The creation instant.</param>
    /// <param name="lifetime">How long it is offered before it expires.</param>
    public static CaptureSuggestion FromAlert(
        ParsedAlert alert,
        string fingerprint,
        Guid? containerId,
        DateOnly today,
        DateTimeOffset now,
        TimeSpan lifetime) => new()
        {
            ParseRuleId = alert.RuleId,
            Fingerprint = fingerprint,
            AmountMinor = alert.AmountMinor,
            CurrencyCode = alert.CurrencyCode,
            Direction = alert.Direction,
            ContainerId = containerId,
            CounterpartyText = alert.Counterparty,
            OccurredOn = alert.OccurredOn ?? today,
            OccurredAtTime = alert.OccurredAtTime,
            Evidence = alert.Channel.AsEvidence(),
            PaymentAppName = alert.AppName,
            Note = alert.Note,
            State = SuggestionState.Pending,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        };

    /// <summary>
    /// Folds a second alert about the same payment into this suggestion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two channels know different things. The bank's SMS knows which card or account
    /// paid, by its last four digits, but names the payee as a VPA or a merchant code. The
    /// Payment App knows the payee's real name and the note, but not which account it
    /// debited. Merged, they are one suggestion that has both.
    /// </para>
    /// <para>
    /// Each fact comes from the channel that knows it best, whichever arrived first. The
    /// payee's name comes from the Payment App, because it is the name the user typed or
    /// saw. The date comes from the bank, because it is the bank's own record. A card-bill
    /// payment the app recognised turns the bank's Debit into a Self Transfer, because
    /// paying a card from a bank is moving your own money, not spending it.
    /// </para>
    /// </remarks>
    /// <param name="alert">The second alert.</param>
    /// <param name="fingerprint">Its fingerprint, remembered so a repost is a duplicate.</param>
    /// <param name="containerId">The container its last four digits matched, if any.</param>
    public void Corroborate(ParsedAlert alert, string fingerprint, Guid? containerId)
    {
        Evidence |= alert.Channel.AsEvidence();
        CorroboratingFingerprint = fingerprint;

        ContainerId ??= containerId;
        PaymentAppName ??= alert.AppName;
        Note ??= alert.Note;
        OccurredAtTime ??= alert.OccurredAtTime;

        if (alert.Channel == AlertChannel.PaymentApp && !string.IsNullOrWhiteSpace(alert.Counterparty))
        {
            CounterpartyText = alert.Counterparty;
        }
        else
        {
            CounterpartyText ??= alert.Counterparty;
        }

        if (alert.Channel == AlertChannel.Sms && alert.OccurredOn is { } bankDate)
        {
            OccurredOn = bankDate;
        }

        if (alert.Direction == TransactionKind.SelfTransfer)
        {
            Direction = TransactionKind.SelfTransfer;
        }
    }

    /// <summary>
    /// Builds the duplicate-suppression fingerprint.
    /// </summary>
    /// <remarks>
    /// SHA-256 over the identifying facts. One-way on purpose: the sender is an input, not
    /// something to be recovered, so this cannot become a record of who messaged the user.
    /// </remarks>
    /// <param name="sender">The SMS originating address.</param>
    /// <param name="sentAt">When the message arrived.</param>
    /// <param name="amountMinor">The parsed amount.</param>
    /// <param name="last4">The parsed last four digits, if any.</param>
    public static string ComputeFingerprint(
        string sender,
        DateTimeOffset sentAt,
        long amountMinor,
        string? last4)
    {
        // Truncated to the minute: the same alert re-delivered seconds later is the same
        // payment, and treating it as a second one would offer the user a duplicate.
        var minute = sentAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        var material = $"{sender.Trim().ToUpperInvariant()}|{minute}|{amountMinor}|{last4 ?? string.Empty}";

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}
