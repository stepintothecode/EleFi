using System.Globalization;
using System.Text;

namespace EleFi.Domain.Money;

/// <summary>
/// Turns <see cref="Money"/> into text and back. The only place money is formatted or
/// parsed, so <c>parse(format(m)) == m</c> is one module's problem rather than everyone's.
/// </summary>
public static class MoneyText
{
    /// <summary>
    /// Formats for display, with Indian lakh/crore grouping under <c>en-IN</c>.
    /// </summary>
    /// <param name="money">The amount.</param>
    /// <param name="withSymbol">Prefix the currency symbol.</param>
    /// <param name="indianGrouping">Group as 1,23,456 rather than 123,456.</param>
    public static string ToDisplayString(Money money, bool withSymbol = true, bool indianGrouping = true)
    {
        var negative = money.Minor < 0;
        var minor = System.Math.Abs(money.Minor);
        var scale = money.Currency.MinorUnitsPerMajor;

        var major = minor / scale;
        var fraction = minor % scale;

        var digits = indianGrouping
            ? GroupIndian(major)
            : major.ToString("N0", CultureInfo.InvariantCulture);

        var text = new StringBuilder();
        if (negative)
        {
            text.Append('-');
        }

        if (withSymbol)
        {
            text.Append(SymbolFor(money.Currency));
        }

        text.Append(digits);

        if (money.Currency.Exponent > 0)
        {
            text.Append('.').Append(fraction.ToString(CultureInfo.InvariantCulture)
                .PadLeft(money.Currency.Exponent, '0'));
        }

        return text.ToString();
    }

    /// <summary>
    /// The round-trip form: a bare decimal, a space, then the code. <c>450.50 INR</c>.
    /// </summary>
    /// <remarks>
    /// This is what <see cref="Parse"/> reads and what the CSV export writes, so a value
    /// that leaves the app can come back unchanged.
    /// </remarks>
    /// <param name="money">The amount.</param>
    public static string ToInvariantString(Money money) =>
        $"{ToBareDecimal(money)} {money.Currency.Code}";

    /// <summary>
    /// The amount alone, with no symbol and no grouping: <c>450.50</c>, or <c>-450.50</c>.
    /// </summary>
    /// <remarks>
    /// FR-7.5: the CSV writes this so a spreadsheet treats the column as numbers rather
    /// than text that needs cleaning first.
    /// </remarks>
    /// <param name="money">The amount.</param>
    public static string ToBareDecimal(Money money)
    {
        var negative = money.Minor < 0;
        var minor = System.Math.Abs(money.Minor);
        var scale = money.Currency.MinorUnitsPerMajor;

        var major = (minor / scale).ToString(CultureInfo.InvariantCulture);
        var sign = negative ? "-" : string.Empty;

        if (money.Currency.Exponent == 0)
        {
            return sign + major;
        }

        var fraction = (minor % scale).ToString(CultureInfo.InvariantCulture)
            .PadLeft(money.Currency.Exponent, '0');

        return $"{sign}{major}.{fraction}";
    }

    /// <summary>
    /// Reads an amount written as a decimal, in the given currency.
    /// </summary>
    /// <remarks>
    /// Accepts grouping separators and a currency symbol, because people paste text from
    /// bank messages. Rejects anything with more decimal places than the currency has,
    /// rather than rounding it: silently dropping a digit off an amount is how a tracker
    /// stops matching a statement.
    /// </remarks>
    /// <param name="text">The text to read.</param>
    /// <param name="currency">The currency to interpret it in.</param>
    /// <param name="money">The parsed amount, when this returns true.</param>
    public static bool TryParse(string? text, Currency currency, out Money money)
    {
        money = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var token = MoneyTextParsing.ExtractNumber(text, out var negative);
        if (token is null || !MoneyTextParsing.ToMinor(token, currency, out var minor))
        {
            return false;
        }

        money = Money.SignedMinor(negative ? -minor : minor, currency);
        return true;
    }

    /// <summary>
    /// Reads an amount, throwing when the text is not a valid amount.
    /// </summary>
    /// <param name="text">The text to read.</param>
    /// <param name="currency">The currency to interpret it in.</param>
    /// <exception cref="FormatException">The text is not a valid amount.</exception>
    public static Money Parse(string text, Currency currency) =>
        TryParse(text, currency, out var money)
            ? money
            : throw new FormatException($"'{text}' is not a valid {currency.Code} amount.");

    /// <summary>The display symbol for a currency, falling back to its code.</summary>
    /// <param name="currency">The currency.</param>
    public static string SymbolFor(Currency currency) => currency.Code switch
    {
        // No symbol for an unspecified currency: "0.00" reads fine while a screen loads,
        // and inventing a rupee sign there would be a small lie about a real number.
        "" => string.Empty,
        "INR" => "₹",
        "USD" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "JPY" => "¥",
        _ => currency.Code + " ",
    };

    // 1,23,45,678: the last three digits, then pairs. Not what "N0" does in any culture
    // .NET ships, so it is written out rather than delegated.
    private static string GroupIndian(long value)
    {
        var digits = value.ToString(CultureInfo.InvariantCulture);
        if (digits.Length <= 3)
        {
            return digits;
        }

        var head = digits[..^3];
        var tail = digits[^3..];

        var grouped = new StringBuilder();
        var count = 0;
        for (var i = head.Length - 1; i >= 0; i--)
        {
            grouped.Insert(0, head[i]);
            count++;
            if (count % 2 == 0 && i > 0)
            {
                grouped.Insert(0, ',');
            }
        }

        return $"{grouped},{tail}";
    }
}
