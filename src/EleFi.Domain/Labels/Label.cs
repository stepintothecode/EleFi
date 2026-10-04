using EleFi.Domain.Primitives;

namespace EleFi.Domain.Labels;

/// <summary>
/// A category attached to a transaction: Food, Travel, Rent, Salary.
/// </summary>
/// <remarks>
/// <para>
/// A transaction carries <b>any number</b> of labels, the way a ticket carries tags. They
/// are flat: no parents, no children, no side restricting which kind of transaction they
/// suit. See <see href="../../../docs/adr/0013-multiple-flat-labels.md">ADR-0013</see>,
/// which reversed the original one-label design.
/// </para>
/// <para>
/// <b>The consequence, stated where it cannot be missed:</b> per-label totals no longer
/// partition spending. A 2,000 shop labelled both Food and Household contributes 2,000 to
/// each, so those two figures sum to 4,000 against 2,000 of real money. Every place that
/// shows per-label figures must therefore compute the overall total independently and must
/// never present the label breakdown as a share of it.
/// </para>
/// </remarks>
public class Label : Entity
{
    /// <summary>What the user calls it: "Food", "Groceries".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>An icon name for the UI.</summary>
    public string? Icon { get; set; }

    /// <summary>A hex colour for chips and charts.</summary>
    public string? Colour { get; set; }

    /// <summary>Ordering in the picker.</summary>
    public int SortOrder { get; set; }

    /// <summary>Normalises a label name so casing and stray spaces do not fragment the list.</summary>
    /// <param name="name">The raw name.</param>
    public static string Normalise(string name) => name.Trim();
}
