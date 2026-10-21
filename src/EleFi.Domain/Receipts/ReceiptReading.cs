namespace EleFi.Domain.Receipts;

/// <summary>The facts on a receipt that a transaction can agree or disagree with.</summary>
/// <param name="Payee">Who was paid.</param>
/// <param name="AmountMinor">The amount or total, in minor units.</param>
/// <param name="OccurredOn">The day.</param>
/// <param name="AccountLast4">The last four digits of the account paid from.</param>
public sealed record ReceiptFacts(string? Payee, long? AmountMinor, DateOnly? OccurredOn, string? AccountLast4);

/// <summary>One line item read from a shop receipt.</summary>
/// <param name="Name">What it was.</param>
/// <param name="AmountMinor">What it cost, in minor units.</param>
public sealed record ReadItem(string Name, long AmountMinor);

/// <summary>Everything read from one receipt, before it is attached to anything.</summary>
/// <param name="Kind">A payment app screen or a shop receipt.</param>
/// <param name="Facts">Payee, amount, day and account.</param>
/// <param name="OccurredAtTime">The time, when the receipt shows one.</param>
/// <param name="AppName">The payment app, when recognised.</param>
/// <param name="Reference">The UPI or bank reference.</param>
/// <param name="Note">A payment note.</param>
/// <param name="Items">Line items, for a shop receipt.</param>
public sealed record ReceiptReading(
    ReceiptKind Kind,
    ReceiptFacts Facts,
    TimeOnly? OccurredAtTime = null,
    string? AppName = null,
    string? Reference = null,
    string? Note = null,
    IReadOnlyList<ReadItem>? Items = null)
{
    /// <summary>The line items read, or none.</summary>
    public IReadOnlyList<ReadItem> LineItems => Items ?? [];
}
