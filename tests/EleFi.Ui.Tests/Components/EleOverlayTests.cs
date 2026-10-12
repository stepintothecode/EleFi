using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Ui.Components;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components;

/// <summary>
/// Ele on screen: floating beside the content, speaking, with a cross, a short sound, and
/// never in the way.
/// </summary>
public class EleOverlayTests : Bunit.TestContext
{
    private readonly Fakes.MemorySettings _settings = new();
    private readonly ManualTime _time = new();
    private readonly Fakes.RecordingSound _sound = new();
    private readonly EleReactions _ele;

    public EleOverlayTests()
    {
        _ele = new EleReactions(_settings, _time);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_ele);
        Services.AddSingleton<ISoundCue>(_sound);
        Services.AddTransient<IMascotService, Fakes.SilentMascot>();
    }

    [Fact]
    public void Nothing_is_drawn_until_something_happens()
    {
        var overlay = RenderComponent<EleOverlay>();

        Assert.Empty(overlay.FindAll(".ele-pop"));
    }

    [Fact]
    public void A_change_brings_ele_on_saying_what_happened()
    {
        var overlay = RenderComponent<EleOverlay>();

        _ele.React(EleMoment.Recorded, "₹450.00", "Zomato");

        overlay.WaitForAssertion(() =>
        {
            var bubble = overlay.Find(".ele-bubble");
            Assert.Contains(_ele.Current!.Line, bubble.TextContent, StringComparison.Ordinal);
            Assert.Contains("Zomato", bubble.TextContent, StringComparison.Ordinal);

            // In an amount span, so Hide amounts covers Ele's words too.
            Assert.Equal("₹450.00", bubble.QuerySelector(".amount")!.TextContent);
            Assert.NotNull(overlay.Find("canvas.mascot"));
        });
    }

    [Fact]
    public void Each_tone_has_its_own_look()
    {
        var overlay = RenderComponent<EleOverlay>();

        _ele.Problem("Who was it paid to?");

        overlay.WaitForAssertion(() => Assert.Contains("ele-cross", overlay.Find(".ele-pop").ClassName, StringComparison.Ordinal));
    }

    [Fact]
    public void Ele_never_blocks_a_tap_on_the_app_except_on_its_own_cross()
    {
        var overlay = RenderComponent<EleOverlay>();
        _ele.React(EleMoment.Updated);

        overlay.WaitForAssertion(() =>
        {
            Assert.Contains("pointer-events: none", overlay.Find(".ele-stage").GetAttribute("style"), StringComparison.Ordinal);
            Assert.Contains("pointer-events: none", overlay.Find(".ele-pop").GetAttribute("style"), StringComparison.Ordinal);
            Assert.Contains("pointer-events: auto", overlay.Find(".ele-close").GetAttribute("style"), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void The_cross_closes_ele_straight_away()
    {
        var overlay = RenderComponent<EleOverlay>();
        _ele.React(EleMoment.Deleted);
        overlay.WaitForAssertion(() => Assert.NotNull(overlay.Find(".ele-close")));

        Assert.Equal("Close Ele", overlay.Find(".ele-close").GetAttribute("aria-label"));
        overlay.Find(".ele-close").Click();

        Assert.Null(_ele.Current);

        // It fades down and away rather than blinking out, and takes no taps while it does.
        overlay.WaitForAssertion(() => Assert.NotNull(overlay.Find(".ele-pop.ele-leaving")));
        Assert.Empty(overlay.FindAll(".ele-close"));

        _time.Advance(EleReactions.LeaveDuration);
        overlay.WaitForAssertion(() => Assert.Empty(overlay.FindAll(".ele-pop")));
    }

    [Fact]
    public void A_new_reaction_comes_in_while_the_old_one_slides_down_and_fades()
    {
        var overlay = RenderComponent<EleOverlay>();
        _ele.React(EleMoment.Recorded);
        _ele.React(EleMoment.Updated);

        overlay.WaitForAssertion(() =>
        {
            var pops = overlay.FindAll(".ele-pop");
            Assert.Equal(2, pops.Count);
            Assert.Single(overlay.FindAll(".ele-pop.ele-leaving"));

            // Only the one arriving can be closed.
            Assert.Single(overlay.FindAll(".ele-close"));
        });

        _time.Advance(EleReactions.LeaveDuration);
        overlay.WaitForAssertion(() => Assert.Single(overlay.FindAll(".ele-pop")));
    }

    [Fact]
    public void Each_reaction_plays_its_sound_once()
    {
        var overlay = RenderComponent<EleOverlay>();

        _ele.React(EleMoment.Recorded);
        overlay.WaitForAssertion(() => Assert.Equal([SoundCue.Pleased], _sound.Played));

        _ele.Problem("Enter an amount.");
        overlay.WaitForAssertion(() => Assert.Equal([SoundCue.Pleased, SoundCue.Cross], _sound.Played));

        // A re-render for the old one leaving is not a new reaction.
        _time.Advance(EleReactions.LeaveDuration);
        overlay.Render();
        Assert.Equal(2, _sound.Played.Count);
    }

    [Fact]
    public void With_the_sound_switched_off_ele_is_silent()
    {
        _ele.SetSoundEnabled(false);
        var overlay = RenderComponent<EleOverlay>();

        _ele.React(EleMoment.Deleted);

        overlay.WaitForAssertion(() => Assert.NotNull(overlay.Find(".ele-pop")));
        Assert.Empty(_sound.Played);
    }
}
