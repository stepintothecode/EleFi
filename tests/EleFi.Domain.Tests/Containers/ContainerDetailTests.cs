using EleFi.Domain.Containers;

namespace EleFi.Domain.Tests.Containers;

public class ContainerDetailTests
{
    [Theory]
    [InlineData("HDFC Bank", "4417", "HDFC Bank ••4417")]
    [InlineData("HDFC Bank", null, "HDFC Bank")]
    [InlineData(null, "4417", "••4417")]
    [InlineData("  ", " ", null)]
    [InlineData(null, null, null)]
    public void The_line_shows_whatever_is_known(string? bank, string? last4, string? expected)
    {
        Assert.Equal(expected, ContainerDetail.Describe(bank, last4));
    }

    [Fact]
    public void A_container_describes_itself_with_only_the_last_four_digits()
    {
        var card = new Container { Name = "Regalia", InstitutionName = "HDFC Bank", AccountNumberLast4 = "123456784417" };

        // C1: the setter keeps four digits, so four is all that can ever be shown.
        Assert.Equal("HDFC Bank ••4417", card.Describe());
    }
}
