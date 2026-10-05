using EleFi.Ui.Services;

namespace EleFi.Ui.Tests.Services;

/// <summary>The colour picker's conversions between hex and its sliders.</summary>
public class ColourMathTests
{
    [Theory]
    [InlineData(0, 100, 50, "#ff0000")]
    [InlineData(120, 100, 50, "#00ff00")]
    [InlineData(240, 100, 50, "#0000ff")]
    [InlineData(0, 0, 100, "#ffffff")]
    [InlineData(0, 0, 0, "#000000")]
    public void Known_colours_convert_exactly(int hue, int saturation, int lightness, string hex)
    {
        Assert.Equal(hex, ColourMath.ToHex(new Hsl(hue, saturation, lightness)));
    }

    [Theory]
    [InlineData("#f97316")]
    [InlineData("#0ea5e9")]
    [InlineData("#a855f7")]
    [InlineData("#94a3b8")]
    public void A_stored_colour_survives_a_trip_through_the_sliders_almost_exactly(string hex)
    {
        Assert.True(ColourMath.TryParse(hex, out var hsl));

        var back = ColourMath.ToHex(hsl);

        // Sliders move in whole steps, so a channel may land one or two units away.
        for (var i = 1; i < 7; i += 2)
        {
            var original = Convert.ToInt32(hex.Substring(i, 2), 16);
            var trip = Convert.ToInt32(back.Substring(i, 2), 16);
            Assert.InRange(Math.Abs(original - trip), 0, 4);
        }
    }

    [Theory]
    [InlineData("F97316", "#f97316")]
    [InlineData("#abc", "#aabbcc")]
    [InlineData("  #0EA5E9 ", "#0ea5e9")]
    [InlineData("#12345", null)]
    [InlineData("orange", null)]
    [InlineData(null, null)]
    public void Typed_hex_is_tidied_or_refused(string? typed, string? expected)
    {
        Assert.Equal(expected, ColourMath.Normalise(typed));
    }
}
