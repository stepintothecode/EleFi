using EleFi.Domain.Audit;

namespace EleFi.Infrastructure.Persistence;

/// <summary>The triggers that put a transaction's items into its history.</summary>
public static partial class AuditTriggers
{
    private const string ItemField = ItemAuditFields.Item;
    private const string ItemLabelField = ItemAuditFields.ItemLabel;

    private const string OccurredAt = "CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER)";

    private static readonly string[] ItemTriggerNames =
    [
        "trg_TransactionItems_Added", "trg_TransactionItems_Changed", "trg_TransactionItems_Removed",
        "trg_TransactionItemLabels_Added", "trg_TransactionItemLabels_Removed",
        "trg_Receipts_Attached", "trg_Receipts_Settled", "trg_Receipts_Removed",
    ];

    /// <summary>
    /// Records items being added, changed and removed, against the transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Like the label triggers, these write to the <b>transaction's</b> timeline, as an update,
    /// because that is what the user did: they changed the transaction. An item is recorded as
    /// its name and amount, so "Milk ₹200.00" still reads correctly after it is renamed.
    /// </para>
    /// <para>
    /// The item-label pair only writes while its item exists. When a whole item goes, the
    /// database may remove its labels after the item itself, and then there is no transaction
    /// left to file the entry under; the item's own removal already says what happened.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> ItemTriggers()
    {
        // char(31) is ItemAuditFields.Separator.
        static string Item(string row) => $"COALESCE({row}.Name, 'Unnamed') || char(31) || {row}.AmountMinor";
        const string labelText =
            "COALESCE((SELECT Name FROM TransactionItems WHERE Id = {0}.ItemId), 'Unnamed') || ': ' || (SELECT Name FROM Labels WHERE Id = {0}.LabelId)";

        yield return Entry("trg_TransactionItems_Added", "AFTER INSERT ON TransactionItems", null, "NEW.TransactionId", ItemField, "'nothing'", Item("NEW"));
        yield return Entry(
            "trg_TransactionItems_Changed",
            "AFTER UPDATE ON TransactionItems",
            "OLD.Name IS NOT NEW.Name OR OLD.AmountMinor IS NOT NEW.AmountMinor",
            "NEW.TransactionId",
            ItemField,
            Item("OLD"),
            Item("NEW"));
        yield return Entry("trg_TransactionItems_Removed", "AFTER DELETE ON TransactionItems", null, "OLD.TransactionId", ItemField, Item("OLD"), "'nothing'");

        yield return Entry(
            "trg_TransactionItemLabels_Added",
            "AFTER INSERT ON TransactionItemLabels",
            "EXISTS (SELECT 1 FROM TransactionItems WHERE Id = NEW.ItemId)",
            "(SELECT TransactionId FROM TransactionItems WHERE Id = NEW.ItemId)",
            ItemLabelField,
            "'nothing'",
            string.Format(System.Globalization.CultureInfo.InvariantCulture, labelText, "NEW"));
        yield return Entry(
            "trg_TransactionItemLabels_Removed",
            "AFTER DELETE ON TransactionItemLabels",
            "EXISTS (SELECT 1 FROM TransactionItems WHERE Id = OLD.ItemId)",
            "(SELECT TransactionId FROM TransactionItems WHERE Id = OLD.ItemId)",
            ItemLabelField,
            string.Format(System.Globalization.CultureInfo.InvariantCulture, labelText, "OLD"),
            "'nothing'");

        // A receipt reads as its payee and amount, the way an item does, so "Receipt nothing to
        // Gnani Mart ₹917.38" says which one. Settling and removing are the two later changes.
        const string receipt = "COALESCE(NEW.Payee, NEW.AppName, 'Receipt') || char(31) || COALESCE(NEW.AmountMinor, 0)";
        yield return Entry("trg_Receipts_Attached", "AFTER INSERT ON Receipts", null, "NEW.TransactionId", ItemAuditFields.Receipt, "'nothing'", receipt);
        yield return Entry(
            "trg_Receipts_Settled",
            "AFTER UPDATE ON Receipts",
            "OLD.SettledAt IS NULL AND NEW.SettledAt IS NOT NULL",
            "NEW.TransactionId",
            ItemAuditFields.Receipt,
            receipt,
            "'kept as it is'");
        yield return Entry(
            "trg_Receipts_Removed",
            "AFTER UPDATE ON Receipts",
            "OLD.DeletedAt IS NULL AND NEW.DeletedAt IS NOT NULL",
            "NEW.TransactionId",
            ItemAuditFields.Receipt,
            receipt,
            "'nothing'");
    }

    private static string Entry(string name, string on, string? when, string transactionId, string field, string from, string to) => $"""
        CREATE TRIGGER IF NOT EXISTS {name}
        {on}
        {(when is null ? string.Empty : $"WHEN {when}")}
        BEGIN
            INSERT INTO AuditEvents (Id, EntityType, EntityId, Action, Changes, CaptureSource, OccurredAt)
            VALUES (lower(hex(randomblob(16))), 'Transactions', {transactionId}, 1,
                    '[' || json_object('field', '{field}', 'from', {from}, 'to', {to}) || ']',
                    NULL, {OccurredAt});
        END;
        """;
}
