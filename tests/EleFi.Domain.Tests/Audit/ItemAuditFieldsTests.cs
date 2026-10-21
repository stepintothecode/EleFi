using EleFi.Domain.Audit;

namespace EleFi.Domain.Tests.Audit;

/// <summary>An item recorded in the history as its name and amount, and read back.</summary>
public class ItemAuditFieldsTests
{
    [Theory]
    [InlineData("Milk\u001F20000", "Milk", 20000)]
    [InlineData("Rice: 5kg\u001F45000", "Rice: 5kg", 45000)]
    public void A_recorded_item_reads_back_as_its_name_and_amount(string recorded, string name, long minor)
    {
        Assert.True(ItemAuditFields.TryRead(recorded, out var readName, out var readMinor));
        Assert.Equal(name, readName);
        Assert.Equal(minor, readMinor);
    }

    [Theory]
    [InlineData("nothing")]
    [InlineData("\u001F100")]
    [InlineData("Milk\u001Fabc")]
    public void Anything_else_is_not_an_item(string recorded)
    {
        Assert.False(ItemAuditFields.TryRead(recorded, out _, out _));
    }
}
