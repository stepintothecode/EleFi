using EleFi.Domain.Primitives;

namespace EleFi.Domain.Receipts;

/// <summary>What kind of paper, or screen, a receipt is.</summary>
public enum ReceiptKind
{
    /// <summary>A payment app's transaction screen: GPay, PhonePe, Paytm and the like.</summary>
    PaymentApp = 0,

    /// <summary>A shop's printed or counter receipt, often with line items.</summary>
    Shop = 1,
}

/// <summary>
/// A receipt attached to a transaction: what was read from it, and whether a disagreement
/// with the transaction has been settled (ADR-0018).
/// </summary>
/// <remarks>
/// <para>
/// What was read is kept as read, never rewritten to match the transaction, so a discrepancy
/// stays visible until the user settles it. Discrepancies themselves are not stored:
/// <see cref="ReceiptCheck"/> derives them every time, so correcting the transaction clears
/// them without anyone remembering to.
/// </para>
/// <para>
/// The photo lives in <see cref="ReceiptImage"/>, apart from this row, so listing receipts
/// never reads a photo.
/// </para>
/// </remarks>
public class Receipt : Entity
{
    /// <summary>The longest payee name kept.</summary>
    public const int MaxPayeeLength = 200;

    /// <summary>The longest note kept.</summary>
    public const int MaxNoteLength = 200;

    /// <summary>The longest reference kept.</summary>
    public const int MaxReferenceLength = 40;

    /// <summary>The transaction it is attached to (RC1).</summary>
    public Guid TransactionId { get; set; }

    /// <summary>A payment app screen or a shop receipt.</summary>
    public ReceiptKind Kind { get; set; }

    /// <summary>The payment app it came from, when one was recognised.</summary>
    public string? AppName { get; set; }

    /// <summary>Who was paid, as the receipt names them.</summary>
    public string? Payee { get; set; }

    /// <summary>The amount, or total, in minor units.</summary>
    public long? AmountMinor { get; set; }

    /// <summary>The currency the amount is in.</summary>
    public string CurrencyCode { get; set; } = Money.Currency.Inr.Code;

    /// <summary>The day on the receipt.</summary>
    public DateOnly? OccurredOn { get; set; }

    /// <summary>The time on the receipt.</summary>
    public TimeOnly? OccurredAtTime { get; set; }

    /// <summary>The UPI or bank reference.</summary>
    public string? Reference { get; set; }

    /// <summary>The note on a payment app screen.</summary>
    public string? Note { get; set; }

    /// <summary>The last four digits of the account it was paid from. Never more (C1).</summary>
    public string? AccountLast4 { get; set; }

    /// <summary>
    /// When the user kept the transaction as it was despite a discrepancy, or null while one
    /// is still open (RC5).
    /// </summary>
    public DateTimeOffset? SettledAt { get; set; }

    /// <summary>The facts the transaction is checked against.</summary>
    public ReceiptFacts Facts => new(Payee, AmountMinor, OccurredOn, AccountLast4);

    /// <summary>A receipt from what was read, attached to a transaction.</summary>
    /// <param name="reading">What was read.</param>
    /// <param name="transactionId">The transaction it belongs to.</param>
    public static Receipt From(ReceiptReading reading, Guid transactionId)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return new Receipt
        {
            TransactionId = transactionId,
            Kind = reading.Kind,
            AppName = reading.AppName,
            Payee = Clip(reading.Facts.Payee, MaxPayeeLength),
            AmountMinor = reading.Facts.AmountMinor,
            OccurredOn = reading.Facts.OccurredOn,
            OccurredAtTime = reading.OccurredAtTime,
            Reference = Clip(reading.Reference, MaxReferenceLength),
            Note = Clip(reading.Note, MaxNoteLength),
            AccountLast4 = reading.Facts.AccountLast4,
        };
    }

    private static string? Clip(string? text, int length) =>
        text is null ? null : text.Length <= length ? text : text[..length];
}

/// <summary>
/// A receipt's photo, compressed, and a small thumbnail for lists. Kept apart from
/// <see cref="Receipt"/> so nothing reads a photo it is not about to show.
/// </summary>
public class ReceiptImage
{
    /// <summary>The receipt this is the photo of.</summary>
    public Guid ReceiptId { get; set; }

    /// <summary>The photo as JPEG, at most 1600 px on its longer side.</summary>
    public byte[] Photo { get; set; } = [];

    /// <summary>A JPEG thumbnail, at most 240 px on its longer side.</summary>
    public byte[] Thumbnail { get; set; } = [];
}
