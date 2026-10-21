using System.Globalization;
using EleFi.Domain.Audit;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Transactions;

/// <summary>One field in a history entry, as a person reads it.</summary>
/// <param name="Field">What changed: "Paid to".</param>
/// <param name="From">What it was: "Zomato".</param>
/// <param name="To">What it became: "Swiggy".</param>
public sealed record HistoryChange(string Field, string From, string To);

/// <summary>One entry in a transaction's history, ready to show.</summary>
/// <param name="Action">What happened.</param>
/// <param name="OccurredAt">When, UTC.</param>
/// <param name="Via">How it was recorded, in words, when the trail knows.</param>
/// <param name="Changes">The fields that changed, if any.</param>
public sealed record HistoryEntry(
    AuditAction Action,
    DateTimeOffset OccurredAt,
    string? Via,
    IReadOnlyList<HistoryChange> Changes);

/// <summary>
/// Turns a transaction's audit trail into sentences: names instead of ids, money instead of
/// minor units, days instead of ISO dates.
/// </summary>
/// <remarks>
/// <para>
/// The triggers store what they can see: column names, foreign keys, integers (AU2). A
/// history that says "Paid to 0193f2c4-..." explains nothing, so every id is looked up by
/// name before anyone reads it. A name is resolved at the time of reading, so a payee renamed
/// since shows its current name, and one that cannot be found any more says so in words.
/// </para>
/// <para>
/// Pure, given the names. The lookup itself is the caller's, which keeps this testable
/// without a database.
/// </para>
/// </remarks>
public static class TransactionHistory
{
    /// <summary>What an id that no longer resolves reads as.</summary>
    public const string Unknown = "someone no longer on record";

    private const string Nothing = "nothing";

    private static readonly HashSet<string> IdFields = new(StringComparer.Ordinal)
    {
        "SourcePartyId", "DestinationPartyId", "MarketplaceAppId", "PaymentAppId",
        "ContainerId", "DestinationContainerId",
    };

    /// <summary>The party and app ids a set of entries mentions, each once.</summary>
    /// <param name="events">The audit entries.</param>
    public static IReadOnlyList<Guid> ReferencedIds(IEnumerable<AuditEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return [.. events
            .SelectMany(e => e.ParsedChanges())
            .Where(c => IdFields.Contains(c.Field))
            .SelectMany(c => new[] { c.FromText, c.ToText })
            .Select(text => Guid.TryParse(text, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()];
    }

    /// <summary>The entries, in the order given, as a person reads them.</summary>
    /// <param name="events">The audit entries, newest first.</param>
    /// <param name="names">Display names for every id <see cref="ReferencedIds"/> returned.</param>
    /// <param name="currency">The transaction's currency, for its amounts.</param>
    public static IReadOnlyList<HistoryEntry> Describe(
        IEnumerable<AuditEvent> events,
        IReadOnlyDictionary<Guid, string> names,
        Currency currency)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(names);

        return [.. events.Select(e => new HistoryEntry(
            e.Action,
            e.OccurredAt,
            e.CaptureSource is { } source ? SourceText(source) : null,
            Changes(e, names, currency)))];
    }

    /// <summary>The label for a stored column name.</summary>
    /// <param name="field">The column name.</param>
    public static string FieldLabel(string field) => field switch
    {
        "SourcePartyId" => "Paid from",
        "DestinationPartyId" => "Paid to",
        "SourceAmountMinor" or "DestinationAmountMinor" => "Amount",
        "SourceCurrencyCode" or "DestinationCurrencyCode" => "Currency",
        "OccurredOn" => "Date",
        "OccurredAtTime" => "Time",
        "Description" => "Note",
        "MarketplaceAppId" => "Bought through",
        "PaymentAppId" => "Paid with",
        "NeedsReview" => "Needs review",
        "CaptureSource" => "Recorded via",
        "Labels" => "Label",
        ItemAuditFields.Item => "Item",
        ItemAuditFields.ItemLabel => "Item label",
        ItemAuditFields.Receipt => "Receipt",

        // A plan's own fields, for its history.
        "AmountMinor" => "Amount",
        "DueOn" => "Due",
        "DueTime" => "Time",
        "RepeatFrequency" => "Repeats",
        "RepeatInterval" => "Every",
        "RepeatUntil" => "Until",
        "RepeatCount" => "Times",
        "CompletedAt" => "Done",
        "IsSkipped" => "Skipped",
        "ContainerId" => "Container",
        "DestinationContainerId" => "Into",
        "CounterpartyName" => "Who",
        "RemindOn" => "Reminder",
        _ => field,
    };

    /// <summary>How a transaction was recorded, as the history says it.</summary>
    /// <param name="stored">The capture source as the trail stored it: a number or a name.</param>
    public static string SourceText(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        CaptureSource? source = int.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            && Enum.IsDefined(typeof(CaptureSource), number)
                ? (CaptureSource)number
                : Enum.TryParse<CaptureSource>(stored, out var named) ? named : null;

        return source switch
        {
            CaptureSource.App => "the app",
            CaptureSource.Widget => "quick capture",
            CaptureSource.Notification => "a notification",
            CaptureSource.Bubble => "the bubble",
            CaptureSource.QuickTile => "the Quick Settings tile",
            CaptureSource.ShareSheet => "the share sheet",
            CaptureSource.Sms => "a bank SMS",
            CaptureSource.Import => "an import",
            CaptureSource.Restore => "a restore",
            CaptureSource.PaymentApp => "a payment app",
            _ => stored,
        };
    }

    private static List<HistoryChange> Changes(AuditEvent entry, IReadOnlyDictionary<Guid, string> names, Currency currency) =>
        entry.ParsedChanges()
            .Select(c => new HistoryChange(
                FieldLabel(c.Field),
                Value(c.Field, c.FromText, names, currency),
                Value(c.Field, c.ToText, names, currency)))

            // The two ends carry one amount and one currency when they match (T4), and a line
            // saying the amount changed twice reads as two edits.
            .Distinct()
            .ToList();

    private static string Value(string field, string raw, IReadOnlyDictionary<Guid, string> names, Currency currency)
    {
        if (string.Equals(raw, Nothing, StringComparison.Ordinal))
        {
            return raw;
        }

        if (IdFields.Contains(field))
        {
            return Guid.TryParse(raw, out var id) && names.TryGetValue(id, out var name) ? name : Unknown;
        }

        return field switch
        {
            "SourceAmountMinor" or "DestinationAmountMinor" or "AmountMinor" when long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor) =>
                MoneyText.ToDisplayString(Money.SignedMinor(minor, currency)),
            "OccurredOn" or "DueOn" or "RepeatUntil" or "RemindOn" when DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) =>
                day.ToString("d MMM yyyy", CultureInfo.InvariantCulture),
            "CompletedAt" when long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) =>
                DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture),
            "RepeatFrequency" when int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frequency)
                && Enum.IsDefined(typeof(Domain.Planning.RepeatFrequency), frequency) =>
                ((Domain.Planning.RepeatFrequency)frequency) == Domain.Planning.RepeatFrequency.None ? "never" : ((Domain.Planning.RepeatFrequency)frequency).ToString().ToLowerInvariant(),
            "NeedsReview" or "IsSkipped" => string.Equals(raw, "1", StringComparison.Ordinal) ? "yes" : "no",
            "CaptureSource" => SourceText(raw),
            ItemAuditFields.Item or ItemAuditFields.Receipt when ItemAuditFields.TryRead(raw, out var item, out var itemMinor) =>
                $"{item} {MoneyText.ToDisplayString(Money.SignedMinor(itemMinor, currency))}",
            _ => raw,
        };
    }
}
