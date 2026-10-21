using Bunit;
using EleFi.Domain.Receipts;
using EleFi.Ui.Components.Receipts;

namespace EleFi.Ui.Tests.Components.Receipts;

/// <summary>Rows read from a shop receipt, ticked and corrected before use.</summary>
public class ReadItemsListTests : Bunit.TestContext
{
    private IReadOnlyList<ReadItem> _chosen = [];

    [Fact]
    public void Unticking_a_row_leaves_it_out_and_the_rest_is_unnamed()
    {
        var list = Render();

        list.FindAll(".read-item-on")[1].Change(false);

        Assert.Equal([new ReadItem("Rice", 450_00)], _chosen);
        Assert.Contains("Chosen ₹450.00 · Unnamed ₹50.00", list.Find(".read-items-total").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_misread_price_and_name_can_be_corrected()
    {
        var list = Render();

        list.FindAll(".read-item-amount")[0].Change("440");
        list.FindAll(".read-item-name")[0].Change("Sona masoori");

        Assert.Equal(new ReadItem("Sona masoori", 440_00), _chosen[0]);
    }

    private IRenderedComponent<ReadItemsList> Render() =>
        RenderComponent<ReadItemsList>(p => p
            .Add(c => c.Items, [new ReadItem("Rice", 450_00), new ReadItem("Carry bag", 5_00)])
            .Add(c => c.TotalMinor, 500_00)
            .Add(c => c.ChosenChanged, items => _chosen = items));
}
