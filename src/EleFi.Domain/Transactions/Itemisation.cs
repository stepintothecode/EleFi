namespace EleFi.Domain.Transactions;

/// <summary>One item as an editor holds it, before it is saved.</summary>
/// <param name="Id">The item's identifier, kept across edits.</param>
/// <param name="Name">What it was, or null for the Unnamed remainder.</param>
/// <param name="AmountMinor">What it cost, in minor units.</param>
/// <param name="LabelIds">Its labels.</param>
/// <param name="Note">Optional free text.</param>
public sealed record ItemDraft(Guid Id, string? Name, long AmountMinor, IReadOnlyList<Guid> LabelIds, string? Note = null)
{
    /// <summary>True for the Unnamed remainder.</summary>
    public bool IsUnnamed => Name is null;

    /// <summary>A new named item with no labels.</summary>
    /// <param name="name">What it was.</param>
    /// <param name="amountMinor">What it cost.</param>
    public static ItemDraft Named(string name, long amountMinor) => new(Guid.CreateVersion7(), name, amountMinor, []);

    /// <summary>An item as it is stored.</summary>
    /// <param name="item">The stored item, with its labels loaded.</param>
    public static ItemDraft Of(TransactionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItemDraft(item.Id, item.Name, item.AmountMinor, [.. item.Labels.Select(l => l.LabelId)], item.Note);
    }
}

/// <summary>What balancing a set of items came to.</summary>
/// <param name="Items">The items to keep, the Unnamed remainder last; empty when not itemised.</param>
/// <param name="OverByMinor">How far the named items exceed the total; zero when they fit.</param>
/// <param name="HasEmptyAmount">True when an item has no amount above zero.</param>
public readonly record struct ItemisationOutcome(IReadOnlyList<ItemDraft> Items, long OverByMinor, bool HasEmptyAmount)
{
    /// <summary>True when the items fit the total and every item has an amount.</summary>
    public bool Succeeded => OverByMinor == 0 && !HasEmptyAmount;
}

/// <summary>
/// The rules that keep a transaction's items adding up to it (IT1 to IT4).
/// </summary>
/// <remarks>
/// <para>
/// Pure, so the editor can check an item as it is typed and the service can check it again
/// on save, with the same answer both times.
/// </para>
/// <para>
/// The user names the items; the remainder names itself. Whatever the named items leave of the
/// total becomes one Unnamed item, so the items always add up to the transaction (IT1) without
/// anyone having to type the last one. When they add up exactly, there is no remainder. When
/// they come to more, nothing is changed and the outcome says by how much.
/// </para>
/// </remarks>
public static class Itemisation
{
    /// <summary>
    /// Fits named items to a total, adding or resizing the Unnamed remainder.
    /// </summary>
    /// <param name="totalMinor">The transaction's amount.</param>
    /// <param name="items">The items, named and possibly an existing remainder, in order.</param>
    /// <returns>
    /// The named items in order followed by the remainder, or no items at all when none is
    /// named: a remainder on its own is just the transaction.
    /// </returns>
    public static ItemisationOutcome Balance(long totalMinor, IReadOnlyList<ItemDraft> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var named = items.Where(i => !i.IsUnnamed).ToList();
        if (named.Count == 0)
        {
            return new ItemisationOutcome([], 0, false);
        }

        if (named.Exists(i => i.AmountMinor <= 0))
        {
            return new ItemisationOutcome(items, 0, true);
        }

        var sum = named.Sum(i => i.AmountMinor);
        if (sum > totalMinor)
        {
            return new ItemisationOutcome(items, sum - totalMinor, false);
        }

        if (sum == totalMinor)
        {
            return new ItemisationOutcome(named, 0, false);
        }

        // The remainder keeps its identity and labels across rebalancing, so its history reads
        // "Unnamed ₹300 to ₹120" rather than one removed and another added.
        var rest = items.FirstOrDefault(i => i.IsUnnamed) ?? new ItemDraft(Guid.CreateVersion7(), null, 0, []);
        return new ItemisationOutcome([.. named, rest with { AmountMinor = totalMinor - sum }], 0, false);
    }

    /// <summary>
    /// Itemises a transaction that had none: the first item, and the rest of the money as
    /// Unnamed carrying the labels the whole transaction had.
    /// </summary>
    /// <remarks>
    /// The new item starts unlabelled because it is usually being split off precisely to be
    /// labelled differently; the leftover money keeps the labels it had, so nothing becomes
    /// unlabelled by accident. A first item that takes the whole amount leaves no remainder,
    /// so it is the whole transaction renamed and takes the labels itself.
    /// </remarks>
    /// <param name="totalMinor">The transaction's amount.</param>
    /// <param name="transactionLabelIds">The labels the transaction carries now.</param>
    /// <param name="first">The first item.</param>
    public static ItemisationOutcome Begin(long totalMinor, IReadOnlyList<Guid> transactionLabelIds, ItemDraft first)
    {
        ArgumentNullException.ThrowIfNull(first);

        if (first.AmountMinor == totalMinor && first.LabelIds.Count == 0)
        {
            first = first with { LabelIds = transactionLabelIds };
        }

        var rest = new ItemDraft(Guid.CreateVersion7(), null, 0, transactionLabelIds);
        return Balance(totalMinor, [first, rest]);
    }

    /// <summary>
    /// The labels a transaction takes back when its last named item is removed: the
    /// remainder's, since the remainder was the whole of it again, or with no remainder, the
    /// last item's, since that item was the whole of it.
    /// </summary>
    /// <param name="items">The items before the last named one went.</param>
    public static IReadOnlyList<Guid> LabelsWhenEnded(IReadOnlyList<ItemDraft> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.FirstOrDefault(i => i.IsUnnamed)?.LabelIds
            ?? (items.Count == 1 ? items[0].LabelIds : []);
    }
}
