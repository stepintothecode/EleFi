namespace EleFi.Ui.Services;

/// <summary>What an undo or redo did.</summary>
/// <param name="Done">True when it ran.</param>
/// <param name="Description">What was undone or redone, for Ele to say.</param>
/// <param name="Error">Why it could not be, when it failed.</param>
public readonly record struct UndoOutcome(bool Done, string? Description, string? Error);

/// <summary>
/// Undo and redo, one history per area of the app.
/// </summary>
/// <remarks>
/// <para>
/// Every change a page makes is pushed here with how to reverse it and how to make it again.
/// The arrows at the top of a page undo and redo the latest change in that page's area, so the
/// arrows on Contacts never reach back and undo an edit to a transaction made somewhere else.
/// </para>
/// <para>
/// Each step goes through the same services as the original change, never a special reverse
/// write, so undoing is checked and audited like everything else. A step that fails (its
/// subject was deleted since, say) is dropped and its reason handed back for Ele to say.
/// </para>
/// <para>
/// Scoped to the WebView: the history lasts while the app is open, like a document editor's.
/// </para>
/// </remarks>
public sealed class UndoHistory
{
    /// <summary>Transactions: recorded, edited, deleted, restored, confirmed.</summary>
    public const string Transactions = "transactions";

    /// <summary>Contacts: merges, links, renames, details, notes.</summary>
    public const string Contacts = "contacts";

    /// <summary>Plans: created, edited, ticked off, skipped, deleted.</summary>
    public const string Plans = "plans";

    /// <summary>Goals: created, edited, archived, deleted.</summary>
    public const string Goals = "goals";

    /// <summary>Containers: added, edited, archived, deleted.</summary>
    public const string Containers = "containers";

    /// <summary>Labels: added, renamed, recoloured, deleted.</summary>
    public const string Labels = "labels";

    /// <summary>The most changes remembered per area. Older ones are forgotten.</summary>
    public const int MaximumDepth = 30;

    private readonly Dictionary<string, List<Step>> _undo = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Step>> _redo = new(StringComparer.Ordinal);

    /// <summary>Raised whenever what can be undone or redone changes.</summary>
    public event Action? Changed;

    /// <summary>Remembers a change that has just been made.</summary>
    /// <param name="area">Which history it belongs to.</param>
    /// <param name="description">What it was, in words: "edit to Zomato".</param>
    /// <param name="undo">How to reverse it.</param>
    /// <param name="redo">How to make it again after an undo.</param>
    public void Push(string area, string description, Func<Task> undo, Func<Task> redo)
    {
        ArgumentNullException.ThrowIfNull(undo);
        ArgumentNullException.ThrowIfNull(redo);

        var steps = Stack(_undo, area);
        steps.Add(new Step(description, undo, redo));
        if (steps.Count > MaximumDepth)
        {
            steps.RemoveAt(0);
        }

        // A new change after an undo is a new branch: what was undone cannot come back.
        Stack(_redo, area).Clear();
        Changed?.Invoke();
    }

    /// <summary>True when the area has a change to undo.</summary>
    /// <param name="area">The area.</param>
    public bool CanUndo(string area) => _undo.TryGetValue(area, out var steps) && steps.Count > 0;

    /// <summary>True when the area has an undone change to make again.</summary>
    /// <param name="area">The area.</param>
    public bool CanRedo(string area) => _redo.TryGetValue(area, out var steps) && steps.Count > 0;

    /// <summary>How many changes the area could undo.</summary>
    /// <param name="area">The area.</param>
    public int UndoCount(string area) => _undo.TryGetValue(area, out var steps) ? steps.Count : 0;

    /// <summary>What undo would reverse next, for the button's label.</summary>
    /// <param name="area">The area.</param>
    public string? NextUndo(string area) => CanUndo(area) ? _undo[area][^1].Description : null;

    /// <summary>What redo would make again next, for the button's label.</summary>
    /// <param name="area">The area.</param>
    public string? NextRedo(string area) => CanRedo(area) ? _redo[area][^1].Description : null;

    /// <summary>Reverses the area's latest change.</summary>
    /// <param name="area">The area.</param>
    public Task<UndoOutcome> UndoAsync(string area) => MoveAsync(area, _undo, _redo, step => step.Undo);

    /// <summary>Makes the area's latest undone change again.</summary>
    /// <param name="area">The area.</param>
    public Task<UndoOutcome> RedoAsync(string area) => MoveAsync(area, _redo, _undo, step => step.Redo);

    private static List<Step> Stack(Dictionary<string, List<Step>> stacks, string area)
    {
        if (!stacks.TryGetValue(area, out var steps))
        {
            steps = [];
            stacks[area] = steps;
        }

        return steps;
    }

    private async Task<UndoOutcome> MoveAsync(
        string area,
        Dictionary<string, List<Step>> from,
        Dictionary<string, List<Step>> to,
        Func<Step, Func<Task>> action)
    {
        var source = Stack(from, area);
        if (source.Count == 0)
        {
            return new UndoOutcome(false, null, null);
        }

        var step = source[^1];
        source.RemoveAt(source.Count - 1);

        try
        {
            await action(step)().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Its subject changed underneath it. Offering it again would fail again.
            Stack(to, area).Clear();
            Changed?.Invoke();
            return new UndoOutcome(false, step.Description, ex.Message);
        }

        Stack(to, area).Add(step);
        Changed?.Invoke();
        return new UndoOutcome(true, step.Description, null);
    }

    private sealed record Step(string Description, Func<Task> Undo, Func<Task> Redo);
}
