using EleFi.Domain.Money;

namespace EleFi.Domain.Tests.Money;

/// <summary>
/// Formatting and parsing. The one module where a mistake shows up on every screen.
/// </summary>
public class MoneyTextTests
{
    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(50, "0.50")]
    [InlineData(45050, "450.50")]
    [InlineData(-45050, "-450.50")]
    [InlineData(100000000, "1000000.00")]
    public void Bare_decimal_is_what_a_spreadsheet_can_sum(long minor, string expected)
    {
        var money = EleFi.Domain.Money.Money.SignedMinor(minor, Currency.Inr);

        Assert.Equal(expected, MoneyText.ToBareDecimal(money));
    }

    [Theory]
    [InlineData(12345678900, "12,34,56,789.00")]
    [InlineData(10000000, "1,00,000.00")]
    [InlineData(123456, "1,234.56")]
    [InlineData(99999, "999.99")]
    [InlineData(100, "1.00")]
    public void En_IN_grouping_is_lakhs_and_crores_not_thousands(long minor, string expected)
    {
        var money = EleFi.Domain.Money.Money.SignedMinor(minor, Currency.Inr);

        // No .NET culture ships this grouping, which is why it is written out by hand.
        Assert.Equal("₹" + expected, MoneyText.ToDisplayString(money));
    }

    [Fact]
    public void M3_a_zero_exponent_currency_has_no_decimal_places()
    {
        var yen = EleFi.Domain.Money.Money.FromMinor(1234, Currency.Of("JPY"));

        // Assuming an exponent of 2 would render this as 12.34 and be wrong by a hundred.
        Assert.Equal("1234", MoneyText.ToBareDecimal(yen));
    }

    [Theory]
    [InlineData("450.50", 45050)]
    [InlineData("1,234.56", 123456)]
    [InlineData("Rs. 450.50", 45050)]
    [InlineData("₹1,00,000", 10000000)]
    [InlineData("450", 45000)]
    [InlineData("0.05", 5)]
    public void Parsing_accepts_what_people_actually_paste(string text, long expectedMinor)
    {
        Assert.True(MoneyText.TryParse(text, Currency.Inr, out var money));
        Assert.Equal(expectedMinor, money.Minor);
    }

    [Theory]
    [InlineData("450.505")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1.2.3")]
    public void Parsing_refuses_rather_than_rounding_or_guessing(string text)
    {
        // 450.505 is rejected, not rounded. Silently dropping a digit off an amount is how
        // a tracker stops matching a bank statement.
        Assert.False(MoneyText.TryParse(text, Currency.Inr, out _));
    }

    [Fact]
    public void Round_trip_holds_across_the_range()
    {
        long[] samples = [0, 1, 99, 100, 12345, 45050, 999999999, 100000000000];

        foreach (var minor in samples)
        {
            var original = EleFi.Domain.Money.Money.FromMinor(minor, Currency.Inr);
            var text = MoneyText.ToBareDecimal(original);

            Assert.True(MoneyText.TryParse(text, Currency.Inr, out var parsed), text);
            Assert.Equal(original, parsed);
        }
    }
}
