using EleFi.Domain.Money;

namespace EleFi.Domain.Tests.Money;

/// <summary>
/// Currency, including the default-constructed one that C# lets anybody make.
/// </summary>
public class CurrencyTests
{
    [Fact]
    public void A_default_currency_does_not_throw_when_asked_anything()
    {
        // Used to throw ArgumentNullException from a dictionary lookup on a null code,
        // which crashed the dashboard on its first render: the net-worth tiles formatted
        // a default(NetWorth) before OnInitializedAsync had replaced it, and the whole
        // WebView showed "An unhandled error has occurred".
        //
        // A struct can always be default-constructed, so every member on one has to answer
        // sensibly for that case. There is no constructor to defend.
        var currency = default(Currency);

        Assert.Equal(2, currency.Exponent);
        Assert.Equal(100, currency.MinorUnitsPerMajor);
        Assert.NotNull(currency.Code);
        Assert.NotNull(currency.ToString());
    }

    [Fact]
    public void A_default_money_formats_as_zero_rather_than_throwing()
    {
        var zero = default(EleFi.Domain.Money.Money);

        Assert.Equal("0.00", MoneyText.ToBareDecimal(zero));
        Assert.NotNull(MoneyText.ToDisplayString(zero));
    }

    [Fact]
    public void Default_money_can_be_added_to_real_money()
    {
        // BalanceMath sums into an accumulator, and a default one must not poison it.
        var real = EleFi.Domain.Money.Money.FromMinor(45050, Currency.Inr);

        Assert.Equal(real.Minor, (default(EleFi.Domain.Money.Money) + real).Minor);
    }

    [Theory]
    [InlineData("inr", "INR")]
    [InlineData("  usd  ", "USD")]
    public void Codes_are_normalised(string input, string expected)
    {
        Assert.Equal(expected, Currency.Of(input).Code);
    }

    [Theory]
    [InlineData("RUPEES")]
    [InlineData("IN")]
    [InlineData("1NR")]
    [InlineData("")]
    public void A_code_that_is_not_three_letters_is_refused(string input)
    {
        Assert.False(Currency.TryOf(input, out _));
    }
}
