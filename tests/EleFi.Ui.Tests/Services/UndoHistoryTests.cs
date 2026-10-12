using EleFi.Ui.Services;

namespace EleFi.Ui.Tests.Services;

/// <summary>
/// Undo and redo: one history per area of the app, so the arrows on the Contacts page never
/// undo an edit to a transaction.
/// </summary>
public class UndoHistoryTests
{
    private readonly UndoHistory _history = new();
    private readonly List<string> _ran = [];

    [Fact]
    public void Nothing_to_undo_or_redo_at_first()
    {
        Assert.False(_history.CanUndo(UndoHistory.Transactions));
        Assert.False(_history.CanRedo(UndoHistory.Transactions));
    }

    [Fact]
    public async Task Undo_runs_the_undo_and_then_redo_is_possible()
    {
        Push(UndoHistory.Transactions, "edit to Zomato");

        var outcome = await _history.UndoAsync(UndoHistory.Transactions);

        Assert.True(outcome.Done);
        Assert.Equal("edit to Zomato", outcome.Description);
        Assert.Equal(["undo edit to Zomato"], _ran);
        Assert.False(_history.CanUndo(UndoHistory.Transactions));
        Assert.True(_history.CanRedo(UndoHistory.Transactions));

        await _history.RedoAsync(UndoHistory.Transactions);

        Assert.Equal(["undo edit to Zomato", "redo edit to Zomato"], _ran);
        Assert.True(_history.CanUndo(UndoHistory.Transactions));
    }

    [Fact]
    public async Task Undo_goes_back_newest_first()
    {
        Push(UndoHistory.Transactions, "first");
        Push(UndoHistory.Transactions, "second");

        await _history.UndoAsync(UndoHistory.Transactions);
        await _history.UndoAsync(UndoHistory.Transactions);

        Assert.Equal(["undo second", "undo first"], _ran);
    }

    [Fact]
    public async Task A_new_change_after_an_undo_clears_what_could_have_been_redone()
    {
        Push(UndoHistory.Transactions, "first");
        await _history.UndoAsync(UndoHistory.Transactions);

        Push(UndoHistory.Transactions, "second");

        Assert.False(_history.CanRedo(UndoHistory.Transactions));
    }

    [Fact]
    public async Task Areas_keep_their_own_history()
    {
        Push(UndoHistory.Transactions, "edit");
        Push(UndoHistory.Contacts, "merge");

        await _history.UndoAsync(UndoHistory.Contacts);

        Assert.Equal(["undo merge"], _ran);
        Assert.True(_history.CanUndo(UndoHistory.Transactions));
        Assert.False(_history.CanUndo(UndoHistory.Contacts));
    }

    [Fact]
    public async Task An_undo_that_fails_says_why_and_is_not_offered_again()
    {
        _history.Push(UndoHistory.Plans, "tick of SIP", () => throw new InvalidOperationException("That plan no longer exists."), () => Task.CompletedTask);

        var outcome = await _history.UndoAsync(UndoHistory.Plans);

        Assert.False(outcome.Done);
        Assert.Equal("That plan no longer exists.", outcome.Error);
        Assert.False(_history.CanUndo(UndoHistory.Plans));
        Assert.False(_history.CanRedo(UndoHistory.Plans));
    }

    [Fact]
    public async Task Undo_with_nothing_to_undo_does_nothing()
    {
        var outcome = await _history.UndoAsync(UndoHistory.Goals);

        Assert.False(outcome.Done);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public void Every_change_is_announced_so_the_arrows_can_light_up()
    {
        var changes = 0;
        _history.Changed += () => changes++;

        Push(UndoHistory.Labels, "rename");

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Only_the_most_recent_changes_are_kept()
    {
        for (var i = 0; i < UndoHistory.MaximumDepth + 5; i++)
        {
            Push(UndoHistory.Transactions, $"change {i}");
        }

        Assert.Equal(UndoHistory.MaximumDepth, _history.UndoCount(UndoHistory.Transactions));
    }

    private void Push(string area, string what) =>
        _history.Push(area, what, () => Run($"undo {what}"), () => Run($"redo {what}"));

    private Task Run(string what)
    {
        _ran.Add(what);
        return Task.CompletedTask;
    }
}
