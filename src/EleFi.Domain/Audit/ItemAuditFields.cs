namespace EleFi.Domain.Audit;

/// <summary>
/// How a transaction's history records its items: the field names, and how a name and an
/// amount share one recorded value.
/// </summary>
/// <remarks>
/// Shared by the triggers that write the entries and the history that reads them, so the two
/// cannot drift apart.
/// </remarks>
public static class ItemAuditFields
{
    /// <summary>An item added, changed or removed.</summary>
    public const string Item = "Item";

    /// <summary>A label going on or coming off an item.</summary>
    public const string ItemLabel = "ItemLabel";

    /// <summary>A receipt attached, settled or removed, recorded as its payee and amount like an item.</summary>
    public const string Receipt = "Receipt";

    /// <summary>Separates an item's name from its amount in minor units.</summary>
    public const char Separator = '\u001F';

    /// <summary>Splits a recorded item into its name and amount, when it is one.</summary>
    /// <param name="recorded">The value as the trail holds it.</param>
    /// <param name="name">The item's name.</param>
    /// <param name="amountMinor">Its amount in minor units.</param>
    /// <returns>False for anything else, such as "nothing".</returns>
    public static bool TryRead(string recorded, out string name, out long amountMinor)
    {
        ArgumentNullException.ThrowIfNull(recorded);

        var at = recorded.LastIndexOf(Separator);
        name = at > 0 ? recorded[..at] : string.Empty;
        amountMinor = 0;
        return at > 0 && long.TryParse(recorded.AsSpan(at + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out amountMinor);
    }
}
