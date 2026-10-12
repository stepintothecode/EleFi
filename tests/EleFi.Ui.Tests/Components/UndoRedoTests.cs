using Bunit;
using EleFi.Ui.Components;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components;

/// <summary>The undo and redo arrows at the top of a page.</summary>
public class UndoRedoTests : Bunit.TestContext
{
    private readonly UndoHistory _history = new();
    private readonly EleReactions _ele = new(new Fakes.MemorySettings(), new ManualTime());
    private int _undone;

    public UndoRedoTests()
    {
        Services.AddSingleton(_history);
        Services.AddSingleton(_ele);
    }

    [Fact]
    public void Both_arrows_are_there_and_neither_works_with_nothing_to_undo()
    {
        var arrows = RenderComponent<UndoRedo>(p => p.Add(u => u.Area, UndoHistory.Transactions));

        Assert.True(arrows.Find("button.undo").HasAttribute("disabled"));
        Assert.True(arrows.Find("button.redo").HasAttribute("disabled"));
    }

    [Fact]
    public void A_change_lights_up_undo_and_says_what_it_would_undo()
    {
        var arrows = RenderComponent<UndoRedo>(p => p.Add(u => u.Area, UndoHistory.Transactions));

        Push("edit to Zomato");

        arrows.WaitForAssertion(() => Assert.False(arrows.Find("button.undo").HasAttribute("disabled")));
        Assert.Equal("Undo edit to Zomato", arrows.Find("button.undo").GetAttribute("aria-label"));
    }

    [Fact]
    public void Undoing_runs_it_tells_the_page_and_ele_says_so_calmly()
    {
        var changed = 0;
        Push("edit to Zomato");
        var arrows = RenderComponent<UndoRedo>(p => p
            .Add(u => u.Area, UndoHistory.Transactions)
            .Add(u => u.OnChanged, () => changed++));

        arrows.Find("button.undo").Click();

        arrows.WaitForAssertion(() =>
        {
            Assert.Equal(1, _undone);
            Assert.Equal(1, changed);
            Assert.Equal(EleTone.Calm, _ele.Current!.Tone);
            Assert.Contains("edit to Zomato", _ele.Current.Line, StringComparison.Ordinal);
            Assert.False(arrows.Find("button.redo").HasAttribute("disabled"));
        });
    }

    [Fact]
    public void An_undo_that_fails_has_ele_say_why()
    {
        _history.Push(UndoHistory.Transactions, "delete", () => throw new InvalidOperationException("It is already gone."), () => Task.CompletedTask);
        var arrows = RenderComponent<UndoRedo>(p => p.Add(u => u.Area, UndoHistory.Transactions));

        arrows.Find("button.undo").Click();

        arrows.WaitForAssertion(() =>
        {
            Assert.Equal(EleTone.Cross, _ele.Current!.Tone);
            Assert.Contains("It is already gone.", _ele.Current.Line, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Another_areas_change_leaves_these_arrows_dark()
    {
        var arrows = RenderComponent<UndoRedo>(p => p.Add(u => u.Area, UndoHistory.Contacts));

        Push("edit to Zomato");

        Assert.True(arrows.Find("button.undo").HasAttribute("disabled"));
    }

    private void Push(string what) =>
        _history.Push(UndoHistory.Transactions, what, () =>
        {
            _undone++;
            return Task.CompletedTask;
        }, () => Task.CompletedTask);
}
