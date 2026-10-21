using System.Globalization;
using System.Text.RegularExpressions;
using EleFi.Domain.Money;

namespace EleFi.Domain.Receipts;

/// <summary>
/// The small readings every receipt needs: amounts, a date, a time, a reference, an account's
/// last four digits.
/// </summary>
/// <remarks>
/// Recognised text is noisy, so each reading is narrow and refuses rather than guesses: an
/// amount needs a rupee sign or two decimal places, a date must not be in the future, and an
/// account is only ever its last four digits (C1).
/// </remarks>
public static partial class ReceiptText
{
    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    /// <summary>The first amount written with a rupee sign, Rs or INR: "₹450", "Rs. 1,240.50".</summary>
    /// <param name="line">One line of text.</param>
    /// <param name="currency">The currency, for its decimal places.</param>
    public static long? Rupees(string line, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(line);
        var match = RupeesPattern().Match(line);
        return match.Success ? Minor(match.Groups[1].Value, currency) : null;
    }

    /// <summary>
    /// A line that is nothing but an amount, perhaps after a stray mark: how a payment screen's
    /// big "₹55" reads once text recognition has dropped the rupee sign, as it does.
    /// </summary>
    /// <param name="line">One line of text.</param>
    /// <param name="currency">The currency, for its decimal places.</param>
    public static long? Bare(string line, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(line);
        var match = BarePattern().Match(line.Trim());
        return match.Success ? Minor(match.Groups[1].Value, currency) : null;
    }

    /// <summary>An amount stated in words around it: "Payment of 55 completed", "Paid 450".</summary>
    /// <param name="line">One line of text.</param>
    /// <param name="currency">The currency, for its decimal places.</param>
    public static long? Stated(string line, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(line);
        var match = StatedPattern().Match(line);
        return match.Success ? Minor(match.Groups[1].Value, currency) : null;
    }

    /// <summary>The amount a line ends with, written with two decimals: a receipt row's price.</summary>
    /// <param name="line">One line of text.</param>
    /// <param name="currency">The currency, for its decimal places.</param>
    public static long? Trailing(string line, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(line);
        var match = TrailingPattern().Match(line);
        return match.Success ? Minor(match.Groups[1].Value, currency) : null;
    }

    /// <summary>Every amount on a line written with two decimals.</summary>
    /// <param name="line">One line of text.</param>
    /// <param name="currency">The currency, for its decimal places.</param>
    public static IEnumerable<long> Decimals(string line, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(line);
        return DecimalPattern().Matches(line).Select(m => Minor(m.Value, currency)).OfType<long>();
    }

    /// <summary>The line with its trailing numbers taken off: "Rice 5kg 1 450.00" to "Rice 5kg".</summary>
    /// <param name="line">A receipt row.</param>
    public static string WithoutTrailingNumbers(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return TrailingNumbersPattern().Replace(line, string.Empty).Trim(' ', '-', ':', '.', 'x', 'X', '@', '*');
    }

    /// <summary>A date on the line: 20/10/2026, 20-10-26, 20 Oct 2026, Oct 20, 2026.</summary>
    /// <param name="line">One line of text.</param>
    /// <param name="today">Today; a date after it is a misreading, not a receipt.</param>
    public static DateOnly? Date(string line, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(line);

        var numeric = NumericDatePattern().Match(line);
        if (numeric.Success && Make(numeric.Groups[3].Value, int.Parse(numeric.Groups[2].Value, CultureInfo.InvariantCulture), numeric.Groups[1].Value) is { } day && day <= today)
        {
            return day;
        }

        var dayFirst = DayMonthPattern().Match(line);
        if (dayFirst.Success && MonthOf(dayFirst.Groups[2].Value) is { } m1
            && Make(dayFirst.Groups[3].Value, m1, dayFirst.Groups[1].Value) is { } d1 && d1 <= today)
        {
            return d1;
        }

        var monthFirst = MonthDayPattern().Match(line);
        return monthFirst.Success && MonthOf(monthFirst.Groups[1].Value) is { } m2
            && Make(monthFirst.Groups[3].Value, m2, monthFirst.Groups[2].Value) is { } d2 && d2 <= today
            ? d2
            : null;
    }

    /// <summary>A time on the line: 19:12, 7:12 pm.</summary>
    /// <param name="line">One line of text.</param>
    public static TimeOnly? Time(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var match = TimePattern().Match(line);
        if (!match.Success)
        {
            return null;
        }

        var hour = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var half = match.Groups[3].Value.ToLowerInvariant();
        if (half.Length > 0)
        {
            if (hour is < 1 or > 12)
            {
                return null;
            }

            hour = (hour % 12) + (half == "pm" ? 12 : 0);
        }

        return hour < 24 && minute < 60 ? new TimeOnly(hour, minute) : null;
    }

    /// <summary>A reference number, digits only, when the line holds one of 10 to 23 digits.</summary>
    /// <param name="line">The line after a "UPI transaction ID" label, or the label's own line.</param>
    public static string? Reference(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var match = ReferencePattern().Match(line);
        if (!match.Success)
        {
            return null;
        }

        var digits = string.Concat(match.Value.Where(char.IsAsciiDigit));
        return digits.Length is >= 10 and <= 23 ? digits : null;
    }

    /// <summary>An account's last four digits: "XX4417", "••4417", "ending 4417". Never more (C1).</summary>
    /// <param name="line">One line of text.</param>
    public static string? Last4(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var match = Last4Pattern().Match(line);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>A name as people write it: "GNANI MART" to "Gnani Mart"; mixed case is left alone.</summary>
    /// <param name="name">A name as read.</param>
    public static string Tidy(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return trimmed.Any(char.IsLower) ? trimmed : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant());
    }

    private static long? Minor(string token, Currency currency) =>
        MoneyTextParsing.ToMinor(token.Replace(",", string.Empty, StringComparison.Ordinal), currency, out var minor) && minor > 0 ? minor : null;

    private static int? MonthOf(string name)
    {
        var at = Array.IndexOf(Months, name[..3].ToLowerInvariant());
        return at < 0 ? null : at + 1;
    }

    private static DateOnly? Make(string yearText, int month, string dayText)
    {
        var year = int.Parse(yearText, CultureInfo.InvariantCulture);
        year = year < 100 ? 2000 + year : year;
        var day = int.Parse(dayText, CultureInfo.InvariantCulture);
        return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month) ? new DateOnly(year, month, day) : null;
    }

    [GeneratedRegex(@"(?:₹|\bRs\.?|\bINR)\s*([0-9][0-9,]*(?:\.[0-9]{1,2})?)", RegexOptions.IgnoreCase)]
    private static partial Regex RupeesPattern();

    [GeneratedRegex(@"([0-9][0-9,]*\.[0-9]{2})\s*$")]
    private static partial Regex TrailingPattern();

    // At most two stray characters before the number: a misread rupee sign, never a word.
    [GeneratedRegex(@"^[^0-9A-Za-z]{0,2}[A-Za-z]?\s?([0-9][0-9,]*(?:\.[0-9]{1,2})?)$")]
    private static partial Regex BarePattern();

    [GeneratedRegex(@"\b(?:payment of|paid|amount|sent)\s*(?:₹|rs\.?|inr)?\s*([0-9][0-9,]*(?:\.[0-9]{1,2})?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StatedPattern();

    [GeneratedRegex(@"(?<![0-9.])[0-9][0-9,]*\.[0-9]{2}(?![0-9])")]
    private static partial Regex DecimalPattern();

    [GeneratedRegex(@"(?:\s+[0-9][0-9,]*(?:\.[0-9]+)?)+\s*$")]
    private static partial Regex TrailingNumbersPattern();

    [GeneratedRegex(@"\b([0-9]{1,2})[/\-.]([0-9]{1,2})[/\-.]([0-9]{4}|[0-9]{2})\b")]
    private static partial Regex NumericDatePattern();

    [GeneratedRegex(@"\b([0-9]{1,2})(?:st|nd|rd|th)?[\s\-]+(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*[\s\-,]+([0-9]{4}|[0-9]{2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex DayMonthPattern();

    [GeneratedRegex(@"\b(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\s+([0-9]{1,2}),?\s+([0-9]{4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex MonthDayPattern();

    [GeneratedRegex(@"\b([0-9]{1,2}):([0-9]{2})(?::[0-9]{2})?\s*(am|pm)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();

    [GeneratedRegex(@"[0-9](?:[0-9 ]{8,30})[0-9]")]
    private static partial Regex ReferencePattern();

    [GeneratedRegex(@"(?:[xX*•●.]{2,}\s?|\bending(?:\s+in)?\s+|\ba/?c\s*(?:no\.?)?\s*[xX*•]*\s*|\b(?:bank|card)\s+)([0-9]{4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex Last4Pattern();
}
