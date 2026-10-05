using Bunit;
using EleFi.Ui.Components;

namespace EleFi.Ui.Tests.Components;

/// <summary>The label colour picker: presets, sliders and exact hex.</summary>
public class ColourPickerTests : Bunit.TestContext
{
    [Fact]
    public void It_offers_far_more_than_nine_colours_and_a_tap_picks_one()
    {
        string? picked = null;
        var picker = RenderComponent<ColourPicker>(p => p
            .Add(x => x.Value, "#f97316")
            .Add(x => x.ValueChanged, v => picked = v));

        var presets = picker.FindAll(".swatch-grid .swatch-btn");
        Assert.Equal(24, presets.Count);

        presets[10].Click();

        Assert.Equal(ColourPicker.Presets[10], picked);
    }

    [Fact]
    public void Moving_the_hue_slider_reaches_colours_no_preset_has()
    {
        string? picked = null;
        var picker = RenderComponent<ColourPicker>(p => p
            .Add(x => x.Value, "#ef4444")
            .Add(x => x.ValueChanged, v => picked = v));

        picker.Find("input[aria-label=Hue]").Input("200");

        Assert.NotNull(picked);
        Assert.DoesNotContain(picked, ColourPicker.Presets);
        Assert.Matches("^#[0-9a-f]{6}$", picked);
    }

    [Fact]
    public void A_typed_hex_is_tidied_and_a_bad_one_ignored()
    {
        string? picked = null;
        var picker = RenderComponent<ColourPicker>(p => p
            .Add(x => x.Value, "#ef4444")
            .Add(x => x.ValueChanged, v => picked = v));

        picker.Find("#colour-hex").Change("ABC");
        Assert.Equal("#aabbcc", picked);

        picked = null;
        picker.Find("#colour-hex").Change("orange");
        Assert.Null(picked);
    }
}
