using EleFi.Domain.Transactions;

namespace EleFi.Domain.Tests.Transactions;

/// <summary>Items always add up to their transaction: the Unnamed remainder makes up the rest.</summary>
public class ItemisationTests
{
    private static readonly Guid Food = Guid.NewGuid();
    private static readonly Guid Groceries = Guid.NewGuid();

    [Fact]
    public void An_item_smaller_than_the_total_leaves_an_unnamed_remainder_for_the_rest()
    {
        var milk = ItemDraft.Named("Milk", 200_00);

        var outcome = Itemisation.Balance(500_00, [milk]);

        Assert.True(outcome.Succeeded);
        Assert.Equal(2, outcome.Items.Count);
        Assert.Equal(milk, outcome.Items[0]);
        Assert.True(outcome.Items[1].IsUnnamed);
        Assert.Equal(300_00, outcome.Items[1].AmountMinor);
    }

    [Fact]
    public void Items_that_add_up_exactly_need_no_remainder()
    {
        var rest = new ItemDraft(Guid.NewGuid(), null, 300_00, [Food]);

        var outcome = Itemisation.Balance(500_00, [ItemDraft.Named("Milk", 200_00), ItemDraft.Named("Rice", 300_00), rest]);

        Assert.True(outcome.Succeeded);
        Assert.DoesNotContain(outcome.Items, i => i.IsUnnamed);
        Assert.Equal(500_00, outcome.Items.Sum(i => i.AmountMinor));
    }

    [Fact]
    public void Items_that_come_to_more_than_the_total_are_refused_with_how_far_over()
    {
        var items = new[] { ItemDraft.Named("Milk", 200_00), ItemDraft.Named("Rice", 350_00) };

        var outcome = Itemisation.Balance(500_00, items);

        Assert.False(outcome.Succeeded);
        Assert.Equal(50_00, outcome.OverByMinor);
        Assert.Equal(items, outcome.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void An_item_needs_an_amount_above_zero(long amount)
    {
        var outcome = Itemisation.Balance(500_00, [ItemDraft.Named("Milk", amount)]);

        Assert.False(outcome.Succeeded);
        Assert.True(outcome.HasEmptyAmount);
    }

    [Fact]
    public void The_remainder_keeps_its_identity_labels_and_place_when_it_is_resized()
    {
        var rest = new ItemDraft(Guid.NewGuid(), null, 300_00, [Groceries], "Loose change");

        var outcome = Itemisation.Balance(500_00, [rest, ItemDraft.Named("Milk", 120_00)]);

        var resized = Assert.Single(outcome.Items, i => i.IsUnnamed);
        Assert.Equal(rest.Id, resized.Id);
        Assert.Equal([Groceries], resized.LabelIds);
        Assert.Equal("Loose change", resized.Note);
        Assert.Equal(380_00, resized.AmountMinor);
        Assert.Same(resized, outcome.Items[^1]);
    }

    [Fact]
    public void With_no_named_item_left_the_transaction_is_not_itemised_at_all()
    {
        var outcome = Itemisation.Balance(500_00, [new ItemDraft(Guid.NewGuid(), null, 500_00, [Food])]);

        Assert.True(outcome.Succeeded);
        Assert.Empty(outcome.Items);
    }

    [Fact]
    public void Beginning_moves_the_transactions_labels_onto_the_remainder_and_leaves_the_new_item_bare()
    {
        var outcome = Itemisation.Begin(500_00, [Food, Groceries], ItemDraft.Named("Kettle", 1_200));

        Assert.Empty(outcome.Items[0].LabelIds);
        Assert.Equal([Food, Groceries], outcome.Items[1].LabelIds);
        Assert.Equal(500_00 - 1_200, outcome.Items[1].AmountMinor);
    }

    [Fact]
    public void Ending_gives_the_transaction_back_the_remainders_labels()
    {
        var items = new[] { ItemDraft.Named("Milk", 200_00), new ItemDraft(Guid.NewGuid(), null, 300_00, [Food]) };

        Assert.Equal([Food], Itemisation.LabelsWhenEnded(items));
        Assert.Empty(Itemisation.LabelsWhenEnded([ItemDraft.Named("Milk", 200_00), ItemDraft.Named("Rice", 300_00)]));
    }

    [Fact]
    public void Ending_with_no_remainder_gives_back_the_last_items_labels()
    {
        Assert.Equal([Groceries], Itemisation.LabelsWhenEnded([ItemDraft.Named("Milk", 500_00) with { LabelIds = [Groceries] }]));
    }

    [Fact]
    public void A_first_item_that_takes_the_whole_amount_keeps_the_transactions_labels()
    {
        var outcome = Itemisation.Begin(500_00, [Food], ItemDraft.Named("Rice", 500_00));

        var only = Assert.Single(outcome.Items);
        Assert.Equal([Food], only.LabelIds);
    }

    [Fact]
    public void A_stored_item_becomes_a_draft_with_its_labels()
    {
        var item = new TransactionItem { Name = "Milk", AmountMinor = 200_00, Note = "Two packets" };
        item.Labels.Add(new TransactionItemLabel { ItemId = item.Id, LabelId = Food });

        var draft = ItemDraft.Of(item);

        Assert.Equal(new ItemDraft(item.Id, "Milk", 200_00, draft.LabelIds, "Two packets"), draft);
        Assert.Equal([Food], draft.LabelIds);
    }
}
