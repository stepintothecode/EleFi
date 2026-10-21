using Bunit;
using EleFi.Domain.Labels;
using EleFi.Domain.Transactions;
using EleFi.Ui.Components.Items;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components.Items;

/// <summary>Splitting a transaction into items on the edit screen.</summary>
public class ItemsEditorTests : Bunit.TestContext
{
    private static readonly Label Food = new() { Name = "Food", Colour = "#f0a35e" };
    private static readonly Label Groceries = new() { Name = "Groceries", Colour = "#4fbf8b" };

    private IReadOnlyList<ItemDraft> _items = [];
    private IReadOnlyList<Guid> _transactionLabels = [Groceries.Id];

    public ItemsEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddEleAndUndo();
    }

    [Fact]
    public void Adding_an_item_smaller_than_the_total_leaves_unnamed_with_the_rest_and_the_old_labels()
    {
        var editor = Render(500_00);

        Add(editor, "Milk", "200");

        Assert.Equal(["Milk", null], _items.Select(i => i.Name));
        Assert.Equal([200_00, 300_00], _items.Select(i => i.AmountMinor));
        Assert.Equal([Groceries.Id], _items[1].LabelIds);
        Assert.Empty(_transactionLabels);

        var rows = editor.FindAll(".item-row");
        Assert.Equal("Unnamed", rows[1].QuerySelector(".item-name")!.TextContent);
        Assert.Contains("₹300.00", rows[1].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void An_item_that_would_go_past_the_total_is_refused_by_ele_and_never_added()
    {
        var editor = Render(500_00);

        Add(editor, "Television", "650");

        var ele = Services.GetRequiredService<EleReactions>().Current!;
        Assert.Equal(EleTone.Cross, ele.Tone);
        Assert.Equal("The items come to ₹150.00 more than the ₹500.00 paid.", ele.Line);
        Assert.Empty(_items);
        Assert.NotNull(editor.Find(".item-form"));
    }

    [Fact]
    public void Items_that_add_up_exactly_have_no_unnamed_row()
    {
        var editor = Render(500_00);

        Add(editor, "Milk", "200");
        Add(editor, "Rice", "300");

        Assert.Equal(["Milk", "Rice"], _items.Select(i => i.Name));
        Assert.DoesNotContain(editor.FindAll(".item-row"), r => r.ClassList.Contains("unnamed"));
    }

    [Fact]
    public void Removing_the_last_item_gives_the_labels_back_to_the_whole_transaction()
    {
        var editor = Render(500_00);
        Add(editor, "Milk", "200");

        editor.FindAll(".item-row")[0].Click();
        editor.Find(".item-remove").Click();
        editor.SetParametersAndRender(p => p.Add(e => e.Items, _items).Add(e => e.TransactionLabelIds, _transactionLabels));

        Assert.Empty(_items);
        Assert.Equal([Groceries.Id], _transactionLabels);
    }

    [Fact]
    public void Unnamed_opens_for_labels_and_a_note_but_its_amount_cannot_be_typed_or_removed()
    {
        var editor = Render(500_00);
        Add(editor, "Milk", "200");

        editor.FindAll(".item-row")[1].Click();

        Assert.Empty(editor.FindAll(".item-amount-input"));
        Assert.Empty(editor.FindAll(".item-remove"));
        Assert.NotNull(editor.Find(".item-note-input"));
    }

    [Fact]
    public void A_nameless_item_is_named_by_its_place()
    {
        var editor = Render(500_00);

        Add(editor, string.Empty, "100");

        Assert.Equal("Item 1", _items[0].Name);
    }

    [Fact]
    public void Changing_the_total_resizes_unnamed_and_warns_when_the_items_no_longer_fit()
    {
        var editor = Render(500_00);
        Add(editor, "Milk", "200");

        editor.SetParametersAndRender(p => p.Add(e => e.TotalMinor, 600_00));
        Assert.Contains("₹400.00", editor.FindAll(".item-row")[1].TextContent, StringComparison.Ordinal);

        editor.SetParametersAndRender(p => p.Add(e => e.TotalMinor, 150_00));
        Assert.Equal("The items come to ₹50.00 more than the ₹150.00 paid.", editor.Find(".items-over").TextContent);
    }

    private IRenderedComponent<ItemsEditor> Render(long total) =>
        RenderComponent<ItemsEditor>(p => p
            .Add(e => e.TotalMinor, total)
            .Add(e => e.Labels, [Food, Groceries])
            .Add(e => e.Items, _items)
            .Add(e => e.ItemsChanged, items => _items = items)
            .Add(e => e.TransactionLabelIds, _transactionLabels)
            .Add(e => e.TransactionLabelIdsChanged, ids => _transactionLabels = ids));

    // As the page does: the list it gets back is the list it hands down next.
    private void Add(IRenderedComponent<ItemsEditor> editor, string name, string amount)
    {
        editor.Find(".items-add").Click();
        editor.Find(".item-name-input").Change(name);
        editor.Find(".item-amount-input").Change(amount);
        editor.Find(".item-done").Click();
        editor.SetParametersAndRender(p => p.Add(e => e.Items, _items).Add(e => e.TransactionLabelIds, _transactionLabels));
    }
}
