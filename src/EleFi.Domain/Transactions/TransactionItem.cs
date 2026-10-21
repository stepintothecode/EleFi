namespace EleFi.Domain.Transactions;

/// <summary>
/// One part of an itemised transaction: what it was, what it cost, and its labels.
/// </summary>
/// <remarks>
/// <para>
/// Items split one payment into the things it bought, so a ₹500 shop can count as ₹200 Food
/// and ₹300 Groceries without either label claiming the whole ₹500 (ADR-0017). They change
/// nothing about the money itself: the balance, the kind, the Source and the Destination are
/// the transaction's, never an item's.
/// </para>
/// <para>
/// Part of the transaction rather than an entity of its own, so there is no soft delete
/// here: removing an item is an edit to the transaction, recorded in its history, and undone
/// the way any edit is. Deleting the transaction keeps its items with it.
/// </para>
/// </remarks>
public class TransactionItem
{
    /// <summary>The longest name an item can have.</summary>
    public const int MaxNameLength = 120;

    /// <summary>The longest note an item can have.</summary>
    public const int MaxNoteLength = 200;

    /// <summary>The identifier, kept across edits so the history can say which item changed.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The transaction this is part of.</summary>
    public Guid TransactionId { get; set; }

    /// <summary>What it was, or null for the Unnamed remainder (IT3).</summary>
    public string? Name { get; set; }

    /// <summary>What it cost, in the transaction's source currency. Always positive (IT2).</summary>
    public long AmountMinor { get; set; }

    /// <summary>Free text about this item alone.</summary>
    public string? Note { get; set; }

    /// <summary>Where it sits in the list, from 0. The Unnamed remainder is always last.</summary>
    public int Position { get; set; }

    /// <summary>This item's labels. Any number, including none.</summary>
    public List<TransactionItemLabel> Labels { get; } = [];

    /// <summary>True for the Unnamed remainder: whatever the named items leave of the total.</summary>
    public bool IsUnnamed => Name is null;
}

/// <summary>Joins an item to one of its labels.</summary>
public class TransactionItemLabel
{
    /// <summary>The item.</summary>
    public Guid ItemId { get; set; }

    /// <summary>The label.</summary>
    public Guid LabelId { get; set; }

    /// <summary>Navigation to the label.</summary>
    public Labels.Label? Label { get; set; }
}
