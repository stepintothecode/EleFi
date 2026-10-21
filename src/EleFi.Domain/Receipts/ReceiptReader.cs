using EleFi.Domain.Money;

namespace EleFi.Domain.Receipts;

/// <summary>
/// Reads recognised text as a receipt: a payment app's screen when it uses their words, a
/// shop's receipt otherwise.
/// </summary>
/// <remarks>
/// Pure and on the device, like the parse rules for alerts. What comes back is a suggestion
/// the user sees and corrects before it is attached to anything.
/// </remarks>
public static class ReceiptReader
{
    /// <summary>What the receipt says, or null when there is no amount on it to go by.</summary>
    /// <param name="lines">The recognised rows, top to bottom.</param>
    /// <param name="currency">The home currency.</param>
    /// <param name="today">Today, so a misread future date is refused.</param>
    public static ReceiptReading? Read(IReadOnlyList<string> lines, Currency currency, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var rows = lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        return PaymentScreenReader.Recognises(rows)
            ? PaymentScreenReader.Read(rows, currency, today) ?? ShopReceiptReader.Read(rows, currency, today)
            : ShopReceiptReader.Read(rows, currency, today);
    }
}
