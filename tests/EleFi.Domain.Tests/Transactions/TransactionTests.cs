using EleFi.Domain.Labels;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Tests.Transactions;

/// <summary>A transaction's items: replaced as a difference, and its labels read through them.</summary>
public class TransactionTests
{
    private static readonly Guid Food = Guid.NewGuid();
    private static readonly Guid Home = Guid.NewGuid();

    [Fact]
    public void Replacing_items_keeps_unchanged_ones_and_their_labels_as_the_same_objects()
    {
        var shop = new Transaction { SourceAmountMinor = 500_00 };
        var milk = ItemDraft.Named("Milk", 200_00) with { LabelIds = [Food] };
        shop.ReplaceItems([milk, new ItemDraft(Guid.NewGuid(), null, 300_00, [])]);
        var storedMilk = shop.Items[0];
        var storedLabel = storedMilk.Labels[0];

        shop.ReplaceItems([milk with { LabelIds = [Food, Home] }, ItemDraft.Named("Rice", 300_00)]);

        Assert.Same(storedMilk, shop.Items[0]);
        Assert.Same(storedLabel, storedMilk.Labels[0]);
        Assert.Equal([Food, Home], storedMilk.Labels.Select(l => l.LabelId));
        Assert.Equal(["Milk", "Rice"], shop.Items.Select(i => i.Name));
        Assert.Equal([0, 1], shop.Items.Select(i => i.Position));
        Assert.All(shop.Items, i => Assert.Equal(shop.Id, i.TransactionId));
    }

    [Fact]
    public void Replacing_with_nothing_removes_every_item()
    {
        var shop = new Transaction { SourceAmountMinor = 500_00 };
        shop.ReplaceItems([ItemDraft.Named("Milk", 500_00)]);

        shop.ReplaceItems([]);

        Assert.False(shop.IsItemised);
    }

    [Fact]
    public void Names_and_notes_are_trimmed_and_a_blank_note_is_none()
    {
        var shop = new Transaction { SourceAmountMinor = 500_00 };

        shop.ReplaceItems([new ItemDraft(Guid.NewGuid(), "  Milk ", 500_00, [], "   ")]);

        Assert.Equal("Milk", shop.Items[0].Name);
        Assert.Null(shop.Items[0].Note);
    }

    [Fact]
    public void All_labels_come_from_the_items_once_itemised_each_once()
    {
        var food = new Label { Id = Food, Name = "Food" };
        var home = new Label { Id = Home, Name = "Home" };
        var shop = new Transaction { SourceAmountMinor = 500_00 };
        shop.Labels.Add(new TransactionLabel { LabelId = Home, Label = home });
        Assert.Equal([home], shop.AllLabels);

        shop.Labels.Clear();
        var milk = new TransactionItem { Name = "Milk", AmountMinor = 200_00 };
        milk.Labels.Add(new TransactionItemLabel { LabelId = Food, Label = food });
        var curd = new TransactionItem { Name = "Curd", AmountMinor = 300_00 };
        curd.Labels.Add(new TransactionItemLabel { LabelId = Food, Label = food });
        shop.Items.AddRange([milk, curd]);

        Assert.Equal([food], shop.AllLabels);
    }
}
