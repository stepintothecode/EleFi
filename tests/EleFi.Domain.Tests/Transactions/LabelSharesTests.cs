using EleFi.Domain.Labels;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Tests.Transactions;

/// <summary>How a transaction's money divides among labels, with and without items.</summary>
public class LabelSharesTests
{
    private static readonly Label Food = new() { Name = "Food" };
    private static readonly Label Home = new() { Name = "Home" };

    [Fact]
    public void Without_items_the_whole_amount_counts_under_every_label()
    {
        var shop = Paid(500_00);
        shop.Labels.Add(new TransactionLabel { Label = Food });
        shop.Labels.Add(new TransactionLabel { Label = Home });

        Assert.Equal([new LabelShare(Food, 500_00), new LabelShare(Home, 500_00)], LabelShares.Of(shop));
    }

    [Fact]
    public void Without_labels_the_whole_amount_is_unlabelled()
    {
        Assert.Equal([new LabelShare(null, 500_00)], LabelShares.Of(Paid(500_00)));
    }

    [Fact]
    public void With_items_each_item_counts_under_its_own_labels_and_a_bare_item_is_unlabelled()
    {
        var shop = Paid(500_00);
        shop.Labels.Add(new TransactionLabel { Label = Home });
        shop.Items.Add(Item("Milk", 200_00, Food));
        shop.Items.Add(Item(null, 300_00));

        // The transaction's own label is ignored once it has items (IT4).
        Assert.Equal([new LabelShare(Food, 200_00), new LabelShare(null, 300_00)], LabelShares.Of(shop));
    }

    [Fact]
    public void An_item_with_two_labels_counts_under_both()
    {
        var shop = Paid(500_00);
        shop.Items.Add(Item("Kettle", 500_00, Food, Home));

        Assert.Equal([new LabelShare(Food, 500_00), new LabelShare(Home, 500_00)], LabelShares.Of(shop));
    }

    private static Transaction Paid(long minor) => new() { SourceAmountMinor = minor, DestinationAmountMinor = minor };

    private static TransactionItem Item(string? name, long minor, params Label[] labels)
    {
        var item = new TransactionItem { Name = name, AmountMinor = minor };
        item.Labels.AddRange(labels.Select(l => new TransactionItemLabel { ItemId = item.Id, LabelId = l.Id, Label = l }));
        return item;
    }
}
