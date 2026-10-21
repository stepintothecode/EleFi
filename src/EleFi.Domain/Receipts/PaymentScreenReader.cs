using EleFi.Domain.Money;

namespace EleFi.Domain.Receipts;

/// <summary>
/// Reads a payment app's transaction screen: GPay, PhonePe, Paytm, Amazon Pay, BHIM, CRED.
/// </summary>
/// <remarks>
/// These screens share a shape: the amount large near the top with a rupee sign, "To" or
/// "Paid to" and the payee, a completed line with the date and time, the UPI transaction ID,
/// and often the bank and its last four digits. The reader looks for those labels rather than
/// for any one app's layout, so a screen it has never seen still reads if it uses the words.
/// </remarks>
public static class PaymentScreenReader
{
    private static readonly (string Marker, string App)[] Apps =
    [
        ("google pay", "GPay"), ("gpay", "GPay"), ("phonepe", "PhonePe"), ("paytm", "Paytm"),
        ("amazon pay", "Amazon Pay"), ("bhim", "BHIM"), ("cred", "CRED"),
    ];

    private static readonly string[] Markers =
        ["upi transaction id", "upi ref", "utr", "transaction id", "paid to", "payment successful", "sent to", "upi id"];

    private static readonly string[] PayeeLabels = ["paid to", "sent to", "to:", "to "];

    // Strongest first: the UPI reference (UTR) is what the bank statement shows, where an app's
    // own transaction id is not.
    private static readonly string[] ReferenceLabels = ["upi transaction id", "upi ref", "utr", "ref no", "reference", "transaction id"];

    private static readonly string[] NoteLabels = ["note", "message", "remarks"];

    private static readonly string[] Statuses = ["completed", "successful", "success", "pending", "failed", "paid", "processing"];

    /// <summary>True when the text looks like a payment app's screen rather than paper.</summary>
    /// <param name="lines">The recognised rows.</param>
    public static bool Recognises(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.Any(l => Markers.Any(m => l.Contains(m, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>What the screen says, or null when it shows no amount.</summary>
    /// <param name="lines">The recognised rows, top to bottom.</param>
    /// <param name="currency">The home currency.</param>
    /// <param name="today">Today, so a misread future date is refused.</param>
    public static ReceiptReading? Read(IReadOnlyList<string> lines, Currency currency, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(lines);

        // The rupee sign first; text recognition often drops it, so then the big amount on a
        // line of its own, then one stated in words ("Payment of 55 completed").
        var amountAt = lines.ToList().FindIndex(l => ReceiptText.Rupees(l, currency) is not null);
        if (amountAt < 0)
        {
            amountAt = lines.ToList().FindIndex(l => ReceiptText.Bare(l, currency) is not null);
        }

        var amount = amountAt >= 0
            ? ReceiptText.Rupees(lines[amountAt], currency) ?? ReceiptText.Bare(lines[amountAt], currency)
            : lines.Select(l => ReceiptText.Stated(l, currency)).FirstOrDefault(a => a is not null);
        if (amount is null)
        {
            return null;
        }

        var dated = lines.FirstOrDefault(l => ReceiptText.Date(l, today) is not null);
        var facts = new ReceiptFacts(
            Labelled(lines, PayeeLabels) is { } payee ? ReceiptText.Tidy(payee) : null,
            amount,
            dated is null ? null : ReceiptText.Date(dated, today),
            lines.Select(ReceiptText.Last4).FirstOrDefault(l => l is not null));

        return new ReceiptReading(
            ReceiptKind.PaymentApp,
            facts,
            dated is null ? null : ReceiptText.Time(dated) ?? lines.Select(ReceiptText.Time).FirstOrDefault(t => t is not null),
            AppName: Apps.FirstOrDefault(a => lines.Any(l => l.Contains(a.Marker, StringComparison.OrdinalIgnoreCase))).App,
            Reference: ReferenceFrom(lines),
            Note: Labelled(lines, NoteLabels) ?? NoteUnder(lines, amountAt));
    }

    // GPay writes the note, unlabelled, right under the amount: "₹55", then "buns".
    private static string? NoteUnder(IReadOnlyList<string> lines, int amountAt)
    {
        if (amountAt < 0 || amountAt + 1 >= lines.Count)
        {
            return null;
        }

        var next = lines[amountAt + 1].Trim();
        return next.Length is > 0 and <= 60 && next.Any(char.IsLetter) && !next.Any(char.IsDigit)
            && !Statuses.Any(s => next.Contains(s, StringComparison.OrdinalIgnoreCase))
            ? next
            : null;
    }

    // The text after a label on its own line, or the next line when the label stands alone.
    private static string? Labelled(IReadOnlyList<string> lines, string[] labels)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            var label = labels.FirstOrDefault(l => line.StartsWith(l, StringComparison.OrdinalIgnoreCase));
            if (label is null)
            {
                continue;
            }

            var rest = line[label.Length..].Trim(' ', ':', '-');
            if (rest.Length > 0)
            {
                return rest;
            }

            if (i + 1 < lines.Count && lines[i + 1].Trim().Length > 0)
            {
                return lines[i + 1].Trim();
            }
        }

        return null;
    }

    private static string? ReferenceFrom(IReadOnlyList<string> lines)
    {
        foreach (var label in ReferenceLabels)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (!lines[i].Contains(label, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var reference = ReceiptText.Reference(lines[i]) ?? (i + 1 < lines.Count ? ReceiptText.Reference(lines[i + 1]) : null);
                if (reference is not null)
                {
                    return reference;
                }
            }
        }

        return null;
    }
}
