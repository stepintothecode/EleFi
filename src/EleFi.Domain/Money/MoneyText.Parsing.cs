using System.Globalization;

namespace EleFi.Domain.Money;

/// <summary>
/// The parsing half of <see cref="MoneyText"/>.
/// </summary>
public static class MoneyTextParsing
{
    /// <summary>
    /// Finds the numeric token in a string that may also carry a symbol, a code, and
    /// grouping separators.
    /// </summary>
    /// <remarks>
    /// Scanning for the number rather than stripping non-digits is what makes
    /// <c>"Rs. 450.50"</c> work. Stripping first leaves <c>".450.50"</c>, which has two
    /// decimal points and is rejected: the full stop after "Rs" is punctuation, not a
    /// separator. Bank messages are full of that shape, so getting it wrong would fail on
    /// the most common format there is.
    /// </remarks>
    /// <param name="text">The text to scan.</param>
    /// <param name="negative">True when a minus sign preceded the number.</param>
    /// <returns>The numeric token with grouping removed, or null when there is none.</returns>
    public static string? ExtractNumber(string text, out bool negative)
    {
        ArgumentNullException.ThrowIfNull(text);
        negative = false;

        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsAsciiDigit(text[i]))
            {
                start = i;
                break;
            }
        }

        if (start < 0)
        {
            return null;
        }

        // A minus belongs to the number only if nothing but whitespace and symbols sit
        // between it and the first digit.
        for (var i = start - 1; i >= 0; i--)
        {
            var c = text[i];
            if (c == '-')
            {
                negative = true;
                break;
            }

            if (char.IsAsciiDigit(c) || char.IsAsciiLetter(c))
            {
                break;
            }
        }

        var end = start;
        var seenDot = false;

        while (end < text.Length)
        {
            var c = text[end];

            if (char.IsAsciiDigit(c) || c == ',')
            {
                end++;
                continue;
            }

            // A dot continues the number only when a digit follows it. "450.50" continues;
            // "450." at the end of a sentence does not.
            if (c == '.' && !seenDot && end + 1 < text.Length && char.IsAsciiDigit(text[end + 1]))
            {
                seenDot = true;
                end++;
                continue;
            }

            break;
        }

        // If another decimal point with digits behind it follows immediately, the input was
        // a malformed number ("1.2.3"), not a number followed by prose. Returning the
        // truncated "1.2" would be a plausible wrong answer, which is the one outcome a
        // money parser must never produce.
        if (end < text.Length
            && text[end] == '.'
            && end + 1 < text.Length
            && char.IsAsciiDigit(text[end + 1]))
        {
            return null;
        }

        var token = text[start..end].Replace(",", string.Empty, StringComparison.Ordinal);
        return token.Length == 0 ? null : token;
    }

    /// <summary>
    /// Splits a numeric token into whole and fractional minor units for a currency.
    /// </summary>
    /// <param name="token">A token from <see cref="ExtractNumber"/>.</param>
    /// <param name="currency">The currency, which decides how many decimal places are legal.</param>
    /// <param name="minor">The amount in minor units, when this returns true.</param>
    public static bool ToMinor(string token, Currency currency, out long minor)
    {
        minor = 0;
        ArgumentNullException.ThrowIfNull(token);

        var parts = token.Split('.');
        if (parts.Length > 2)
        {
            return false;
        }

        var wholeText = parts[0].Length == 0 ? "0" : parts[0];
        var fractionText = parts.Length == 2 ? parts[1] : string.Empty;

        // More decimal places than the currency has is refused, never rounded. Silently
        // dropping a digit off an amount is how a tracker stops matching a statement.
        if (fractionText.Length > currency.Exponent)
        {
            return false;
        }

        if (!long.TryParse(wholeText, NumberStyles.None, CultureInfo.InvariantCulture, out var whole))
        {
            return false;
        }

        var fraction = 0L;
        if (fractionText.Length > 0
            && !long.TryParse(
                fractionText.PadRight(currency.Exponent, '0'),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out fraction))
        {
            return false;
        }

        try
        {
            minor = checked((whole * currency.MinorUnitsPerMajor) + fraction);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
