using System.Globalization;
using System.Text.RegularExpressions;
using EleFi.Domain.Money;

namespace EleFi.Domain.Alerts;

/// <summary>What a rule extracted from a message, before anything is persisted.</summary>
/// <param name="RuleId">The rule that matched.</param>
/// <param name="AmountMinor">The amount, in minor units.</param>
/// <param name="CurrencyCode">The currency the amount is in.</param>
/// <param name="Direction">Whether money left or arrived.</param>
/// <param name="Last4">Last four digits of the account or card, if the message carried them.</param>
/// <param name="Counterparty">Merchant or payer text, if the message carried it.</param>
/// <param name="OccurredOn">The date, if the message carried one.</param>
/// <param name="Note">A note or remark the payer attached, if the alert carried one.</param>
/// <param name="Channel">Where the alert arrived from.</param>
/// <param name="AppName">The Payment App it came through, for a Payment App alert.</param>
/// <param name="OccurredAtTime">
/// The local time of day, known only for a Payment App alert. The parser never sets it; the
/// caller does, from when the notification was raised.
/// </param>
public readonly record struct ParsedAlert(
    Guid RuleId,
    long AmountMinor,
    string CurrencyCode,
    Transactions.TransactionKind Direction,
    string? Last4,
    string? Counterparty,
    DateOnly? OccurredOn,
    string? Note = null,
    AlertChannel Channel = AlertChannel.Sms,
    string? AppName = null,
    TimeOnly? OccurredAtTime = null);

/// <summary>
/// Turns a bank's SMS into a <see cref="ParsedAlert"/>, or into nothing.
/// </summary>
/// <remarks>
/// <para>
/// Pure: no I/O, no clock, no database. That is what makes SMS parsing testable without a
/// phone, a SIM, or a permission, and it is why the corpus tests run in milliseconds.
/// </para>
/// <para>
/// The message body arrives as an <see cref="SmsBody"/>, which cannot escape this call.
/// </para>
/// </remarks>
public static class AlertParser
{
    /// <summary>
    /// Patterns that mean "this is a one-time passcode", checked before any rule runs.
    /// </summary>
    /// <remarks>
    /// SM4: an OTP is never parsed, stored, or surfaced, including from a sender a rule
    /// matches. Granting SMS access hands the app the user's second factor, and this is the
    /// gate that means it is never looked at. Deliberately broad: a false negative here
    /// costs one missed suggestion, a false positive costs the user's security.
    /// </remarks>
    private static readonly Regex OtpMarkers = new(
        @"\b(otp|one[\s-]?time[\s-]?(password|passcode|pin)|verification\s+code|"
        + @"security\s+code|auth(entication)?\s+code|do\s+not\s+share|"
        + @"never\s+share|valid\s+for\s+\d+\s+min)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    /// <summary>
    /// Runs the enabled rules against a message and returns the first match.
    /// </summary>
    /// <remarks>
    /// Returns null far more often than not, and that is correct behaviour. A rule that
    /// matches the sender but not the body produces nothing rather than a partial guess
    /// (FR-11.4).
    /// </remarks>
    /// <param name="sender">The SMS originating address.</param>
    /// <param name="body">The message text. Cannot outlive this call.</param>
    /// <param name="rules">Compiled rules, in priority order.</param>
    /// <param name="homeCurrency">Currency to assume when the message does not say.</param>
    /// <returns>The parsed alert, or null when nothing matched or the message is an OTP.</returns>
    public static ParsedAlert? Parse(
        string sender,
        SmsBody body,
        IReadOnlyList<CompiledParseRule> rules,
        Currency homeCurrency) =>
        Parse(AlertChannel.Sms, sender, body, rules, homeCurrency);

    /// <summary>
    /// Runs the enabled rules for one channel against an alert and returns the first match.
    /// </summary>
    /// <remarks>
    /// A Payment App notification is read under exactly the same gates as an SMS: sender
    /// first, then the OTP check, then the rules, and nothing at all when no rule matches.
    /// Only rules written for the alert's own channel are considered.
    /// </remarks>
    /// <param name="channel">Where the alert arrived from.</param>
    /// <param name="sender">The SMS originating address, or the posting app's package.</param>
    /// <param name="body">The alert text. Cannot outlive this call.</param>
    /// <param name="rules">Compiled rules, in priority order.</param>
    /// <param name="homeCurrency">Currency to assume when the alert does not say.</param>
    /// <returns>The parsed alert, or null when nothing matched or the text is an OTP.</returns>
    public static ParsedAlert? Parse(
        AlertChannel channel,
        string sender,
        SmsBody body,
        IReadOnlyList<CompiledParseRule> rules,
        Currency homeCurrency)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (string.IsNullOrWhiteSpace(sender) || body.IsEmpty)
        {
            return null;
        }

        // Gate one: the sender, on this channel. A message from an address no enabled rule
        // recognises is dropped without its body being examined (SM3).
        var candidates = rules
            .Where(r => r.Accepts(channel, sender))
            .OrderBy(r => r.Rule.Priority)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        // Gate two: OTPs, checked once for every rule rather than trusted to each (SM4).
        var text = body.Text.ToString();
        if (OtpMarkers.IsMatch(text))
        {
            return null;
        }

        foreach (var candidate in candidates)
        {
            var match = candidate.Body.Match(text);
            if (!match.Success)
            {
                continue;
            }

            var currency = ReadCurrency(match, homeCurrency);
            if (!MoneyText.TryParse(Group(match, "amount"), currency, out var amount) || amount.Minor <= 0)
            {
                // The rule matched but the amount is unreadable. Produce nothing rather
                // than a suggestion with a wrong number in it.
                continue;
            }

            return new ParsedAlert(
                candidate.Rule.Id,
                amount.Minor,
                currency.Code,
                candidate.Rule.Direction,
                ReadLast4(match),
                Tidy(Group(match, "counterparty")),
                ReadDate(match),
                Tidy(Group(match, "note")),
                channel,
                candidate.Rule.AppName);
        }

        return null;
    }

    /// <summary>
    /// True when this message looks like a one-time passcode.
    /// </summary>
    /// <remarks>
    /// Exposed so the corpus tests can assert SM4 directly against real OTP formats,
    /// rather than inferring it from a rule producing no match for some other reason.
    /// </remarks>
    /// <param name="body">The message text.</param>
    public static bool LooksLikeOtp(SmsBody body) =>
        !body.IsEmpty && OtpMarkers.IsMatch(body.Text.ToString());

    private static string? Group(Match match, string name)
    {
        var group = match.Groups[name];
        return group.Success && group.Value.Length > 0 ? group.Value : null;
    }

    // Trailing punctuation and runs of spaces come from how the alert was worded, not from
    // the name, and would otherwise fragment the party list ("Zomato." and "Zomato").
    private static string? Tidy(string? captured)
    {
        if (captured is null)
        {
            return null;
        }

        var collapsed = string.Join(' ', captured.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var trimmed = collapsed.TrimEnd('.', ',', ';', ':', '!', '-').Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static Currency ReadCurrency(Match match, Currency fallback)
    {
        var raw = Group(match, "currency");
        if (raw is null)
        {
            return fallback;
        }

        var normalised = raw.Trim().ToUpperInvariant() switch
        {
            "RS" or "RS." or "INR" or "₹" => "INR",
            "$" or "USD" => "USD",
            "€" or "EUR" => "EUR",
            "£" or "GBP" => "GBP",
            var other => other,
        };

        return Currency.TryOf(normalised, out var currency) ? currency : fallback;
    }

    private static string? ReadLast4(Match match)
    {
        var raw = Group(match, "last4");
        if (raw is null)
        {
            return null;
        }

        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : digits[^System.Math.Min(4, digits.Length)..];
    }

    private static DateOnly? ReadDate(Match match)
    {
        var raw = Group(match, "occurredOn");
        if (raw is null)
        {
            return null;
        }

        string[] formats =
        [
            "dd-MM-yy", "dd-MM-yyyy", "dd/MM/yy", "dd/MM/yyyy",
            "dd-MMM-yy", "dd-MMM-yyyy", "dd MMM yy", "dd MMM yyyy",
            "yyyy-MM-dd", "MM/dd/yy", "MM/dd/yyyy",
        ];

        return DateOnly.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
    }
}
