using EleFi.Application.Abstractions;
using EleFi.Domain.Labels;

namespace EleFi.Application.Labels;

/// <summary>The outcome of a label change.</summary>
/// <param name="Label">The saved label, when it succeeded.</param>
/// <param name="Error">Why it was refused, when it failed.</param>
public readonly record struct LabelResult(Label? Label, string? Error)
{
    /// <summary>True when the change was saved.</summary>
    public bool Succeeded => Error is null;
}

/// <summary>
/// Creates, renames, and removes labels.
/// </summary>
/// <remarks>
/// Labels are flat, optional, and many per transaction (ADR-0013), which leaves almost
/// nothing for this class to protect beyond names staying distinct. That simplicity is the
/// point: the previous design needed a replacement label on delete, a depth check, a side
/// restriction, and an undeletable system label, and every one of those was a rule the user
/// had to learn.
/// </remarks>
public sealed class LabelService(ILabelRepository labels, IClock clock)
{
    /// <summary>Every live label.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IReadOnlyList<Label>> ListAsync(CancellationToken cancellationToken = default) =>
        labels.ListAsync(cancellationToken);

    /// <summary>
    /// Every live label, the most used first, for the pickers on the capture forms.
    /// </summary>
    /// <remarks>
    /// The picker is a single scrolling row, so only the first few chips are visible
    /// without a swipe. Putting the labels the user actually reaches for there is what
    /// makes the common case one tap. Ties, including every label never used, keep the
    /// user's own sort order.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<Label>> ListByUsageAsync(CancellationToken cancellationToken = default)
    {
        var all = await labels.ListAsync(cancellationToken).ConfigureAwait(false);
        var counts = await labels.UsageCountsAsync(cancellationToken).ConfigureAwait(false);

        return all
            .OrderByDescending(l => counts.TryGetValue(l.Id, out var count) ? count : 0)
            .ThenBy(l => l.SortOrder)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>How many transactions carry a label, so delete can say what it will affect.</summary>
    /// <param name="labelId">The label.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<int> UsageAsync(Guid labelId, CancellationToken cancellationToken = default) =>
        labels.TransactionCountAsync(labelId, cancellationToken);

    /// <summary>Creates a label.</summary>
    /// <param name="name">What to call it.</param>
    /// <param name="colour">A hex colour for chips and charts.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<LabelResult> CreateAsync(
        string name,
        string? colour = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new LabelResult(null, "Give the label a name.");
        }

        if (await labels.NameTakenAsync(name, null, cancellationToken).ConfigureAwait(false))
        {
            return new LabelResult(null, $"There is already a label called '{name.Trim()}'.");
        }

        var now = clock.UtcNow;
        var label = new Label
        {
            Name = Label.Normalise(name),
            Colour = colour,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await labels.AddAsync(label, cancellationToken).ConfigureAwait(false);
        return new LabelResult(label, null);
    }

    /// <summary>Renames a label and changes its colour.</summary>
    /// <param name="id">The label.</param>
    /// <param name="name">The new name.</param>
    /// <param name="colour">A hex colour.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<LabelResult> UpdateAsync(
        Guid id,
        string name,
        string? colour = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new LabelResult(null, "Give the label a name.");
        }

        var label = await labels.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (label is null)
        {
            return new LabelResult(null, "That label no longer exists.");
        }

        if (await labels.NameTakenAsync(name, id, cancellationToken).ConfigureAwait(false))
        {
            return new LabelResult(null, $"There is already a label called '{name.Trim()}'.");
        }

        label.Name = Label.Normalise(name);
        label.Colour = colour;
        label.UpdatedAt = clock.UtcNow;

        await labels.UpdateAsync(label, cancellationToken).ConfigureAwait(false);
        return new LabelResult(label, null);
    }

    /// <summary>
    /// Removes a label and detaches it from every transaction.
    /// </summary>
    /// <remarks>
    /// No replacement needed. A transaction that loses its only label becomes unlabelled,
    /// which is now a normal state rather than an orphan, and the amounts are untouched
    /// either way.
    /// </remarks>
    /// <param name="id">The label to remove.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<LabelResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var label = await labels.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (label is null)
        {
            return new LabelResult(null, "That label no longer exists.");
        }

        await labels.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return new LabelResult(label, null);
    }
}
