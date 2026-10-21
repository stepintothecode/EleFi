using EleFi.Domain.Money;

namespace EleFi.Domain.Receipts;

/// <summary>
/// Reads a shop's printed receipt: the shop's name, the date, the total, and the rows above
/// the total that end in a price.
/// </summary>
/// <remarks>
/// <para>
/// Receipts vary more than payment screens, so this reads only what nearly every receipt has.
/// The name is the first line of words at the top that is not a heading like "Tax Invoice".
/// The total is the amount on the strongest total line ("Grand Total" before "Total"), or the
/// largest amount on the receipt when no line says total.
/// </para>
/// <para>
/// Items are the rows between the name and the total that end in a price and are not tax,
/// discount, payment or column headings. They are suggestions: the user ticks and corrects them
/// before anything is saved.
/// </para>
/// </remarks>
public static class ShopReceiptReader
{
    // Strongest first, so "Grand Total" wins over a "Total" that is really a subtotal.
    private static readonly string[] TotalWords =
        ["grand total", "net payable", "amount payable", "net amount", "total amount", "bill amount", "amount due", "net amt", "total"];

    private static readonly string[] NotItems =
    [
        "total", "subtotal", "sub total", "tax", "gst", "cgst", "sgst", "igst", "vat", "cess", "discount", "round",
        "cash", "change", "card", "upi", "paid", "balance", "tender", "saving", "qty", "mrp", "rate", "hsn",
        "invoice", "bill no", "date", "time", "phone", "tel", "mob", "amount",
    ];

    private static readonly string[] Headings =
        ["tax invoice", "invoice", "gstin", "gst no", "bill", "receipt", "cash memo", "welcome", "phone", "tel", "mob", "fssai"];

    /// <summary>What the receipt says, or null when no amount can be found on it.</summary>
    /// <param name="lines">The recognised rows, top to bottom.</param>
    /// <param name="currency">The home currency.</param>
    /// <param name="today">Today, so a misread future date is refused.</param>
    public static ReceiptReading? Read(IReadOnlyList<string> lines, Currency currency, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var (totalAt, total) = Total(lines, currency);
        if (total is null)
        {
            return null;
        }

        var nameAt = NameLine(lines);
        var dated = lines.FirstOrDefault(l => ReceiptText.Date(l, today) is not null);
        var facts = new ReceiptFacts(
            nameAt is { } at ? ReceiptText.Tidy(lines[at]) : null,
            total,
            dated is null ? null : ReceiptText.Date(dated, today),
            lines.Select(ReceiptText.Last4).FirstOrDefault(l => l is not null));

        return new ReceiptReading(
            ReceiptKind.Shop,
            facts,
            dated is null ? null : ReceiptText.Time(dated),
            Items: Items(lines, (nameAt ?? -1) + 1, totalAt ?? lines.Count, currency));
    }

    private static (int? At, long? Amount) Total(IReadOnlyList<string> lines, Currency currency)
    {
        foreach (var word in TotalWords)
        {
            // From the bottom: the payable total comes after any subtotal.
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                if (!lines[i].Contains(word, StringComparison.OrdinalIgnoreCase) || lines[i].Contains("sub", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var amount = ReceiptText.Decimals(lines[i], currency).Cast<long?>().LastOrDefault()
                    ?? ReceiptText.Rupees(lines[i], currency)
                    ?? (i + 1 < lines.Count ? ReceiptText.Decimals(lines[i + 1], currency).Cast<long?>().LastOrDefault() : null);
                if (amount is not null)
                {
                    return (i, amount);
                }
            }
        }

        var largest = lines.SelectMany(l => ReceiptText.Decimals(l, currency)).DefaultIfEmpty().Max();
        return (null, largest > 0 ? largest : null);
    }

    private static int? NameLine(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < Math.Min(lines.Count, 5); i++)
        {
            var line = lines[i];
            var letters = line.Count(char.IsLetter);
            if (letters >= 3 && letters * 2 > line.Count(c => !char.IsWhiteSpace(c))
                && !Headings.Any(h => line.Contains(h, StringComparison.OrdinalIgnoreCase)))
            {
                return i;
            }
        }

        return null;
    }

    private static List<ReadItem> Items(IReadOnlyList<string> lines, int from, int to, Currency currency)
    {
        var items = new List<ReadItem>();
        for (var i = Math.Max(0, from); i < Math.Min(to, lines.Count); i++)
        {
            var line = lines[i];
            if (NotItems.Any(w => line.Contains(w, StringComparison.OrdinalIgnoreCase)) || ReceiptText.Trailing(line, currency) is not { } price)
            {
                continue;
            }

            var name = ReceiptText.WithoutTrailingNumbers(line);
            if (name.Count(char.IsLetter) >= 2)
            {
                items.Add(new ReadItem(ReceiptText.Tidy(name), price));
            }
        }

        return items;
    }
}
