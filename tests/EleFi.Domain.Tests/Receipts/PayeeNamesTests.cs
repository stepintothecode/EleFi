using EleFi.Domain.Receipts;

namespace EleFi.Domain.Tests.Receipts;

/// <summary>The same payee written the ways receipts, apps and people write it.</summary>
public class PayeeNamesTests
{
    [Theory]
    [InlineData("GNANI MART PVT LTD", "Gnani Mart")]
    [InlineData("gnanimart@okhdfc", "Gnani Mart")]
    [InlineData("Zomato", "ZOMATO LIMITED")]
    [InlineData("Sri Sundha Mobiles", "Sundha Mobiles")]
    public void Different_spellings_of_one_payee_are_the_same(string a, string b) => Assert.True(PayeeNames.Same(a, b));

    [Theory]
    [InlineData("Gnani Mart", "Gajanana Store")]
    [InlineData("Swiggy", "Zomato")]
    [InlineData("Ltd", "Pvt Ltd")]
    public void Different_payees_are_not(string a, string b) => Assert.False(PayeeNames.Same(a, b));
}
