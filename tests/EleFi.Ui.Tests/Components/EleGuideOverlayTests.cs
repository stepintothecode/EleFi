using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Ui.Components;
using EleFi.Ui.Components.Layout;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components;

/// <summary>Ele's guide over the app: the steps, Skip, Previous and Next, back, and the lit tab.</summary>
public class EleGuideOverlayTests : Bunit.TestContext
{
    private readonly EleGuide _guide = new(new Fakes.MemorySettings());
    private readonly Fakes.RecordingSound _sound = new();

    public EleGuideOverlayTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_guide);
        Services.AddSingleton<ISoundCue>(_sound);
        Services.AddEleAndUndo();
        Services.AddSingleton(new PrivacyMode(new Fakes.MemorySettings()));
        Services.AddTransient<IMascotService, Fakes.SilentMascot>();
    }

    [Fact]
    public void Nothing_shows_until_the_guide_starts()
    {
        var overlay = RenderComponent<EleGuideOverlay>();

        Assert.Empty(overlay.FindAll(".guide-backdrop"));
    }

    [Fact]
    public void A_step_is_ele_one_sentence_previous_next_and_skip_at_the_top()
    {
        var overlay = RenderComponent<EleGuideOverlay>();

        overlay.InvokeAsync(_guide.Start);

        Assert.NotNull(overlay.Find(".guide-backdrop canvas"));
        Assert.Equal(EleGuide.Steps[0].Title, overlay.Find(".guide-title").TextContent);
        Assert.Equal($"1 of {EleGuide.Steps.Count}", overlay.Find(".guide-count").TextContent);
        Assert.True(overlay.Find(".guide-previous").HasAttribute("disabled"));
        Assert.Equal("Skip guide", overlay.Find(".guide-skip").TextContent);
    }

    [Fact]
    public void Next_opens_the_steps_page_behind_the_guide()
    {
        var overlay = RenderComponent<EleGuideOverlay>();
        overlay.InvokeAsync(_guide.Start);

        overlay.Find(".guide-next").Click();

        Assert.Equal("Start here", overlay.Find(".guide-title").TextContent);
        Assert.EndsWith("/containers", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        Assert.False(overlay.Find(".guide-previous").HasAttribute("disabled"));
    }

    [Fact]
    public void The_last_step_says_lets_go_and_finishes()
    {
        var overlay = RenderComponent<EleGuideOverlay>();
        overlay.InvokeAsync(_guide.Start);

        for (var i = 1; i < EleGuide.Steps.Count; i++)
        {
            overlay.Find(".guide-next").Click();
        }

        Assert.Equal("Let's go", overlay.Find(".guide-next").TextContent);
        overlay.Find(".guide-next").Click();

        Assert.Empty(overlay.FindAll(".guide-backdrop"));
        Assert.True(_guide.HasFinished);
    }

    [Fact]
    public void Every_sentence_sits_on_its_own_line()
    {
        var overlay = RenderComponent<EleGuideOverlay>();
        overlay.InvokeAsync(_guide.Start);

        Assert.Equal(EleGuide.Sentences(EleGuide.Steps[0].Line).Count, overlay.FindAll(".guide-line").Count);
        Assert.True(overlay.FindAll(".guide-line").Count >= 2);
    }

    [Fact]
    public void Ele_makes_her_sound_on_each_step_a_trumpet_when_happy_a_hum_otherwise()
    {
        var overlay = RenderComponent<EleGuideOverlay>();

        overlay.InvokeAsync(_guide.Start);
        overlay.Find(".guide-next").Click();

        Assert.Equal([SoundCue.Pleased, SoundCue.Calm], _sound.Played);
    }

    [Fact]
    public void Skip_closes_it_from_any_step()
    {
        var overlay = RenderComponent<EleGuideOverlay>();
        overlay.InvokeAsync(_guide.Start);
        overlay.Find(".guide-next").Click();

        overlay.Find(".guide-skip").Click();

        Assert.Empty(overlay.FindAll(".guide-backdrop"));
        Assert.True(_guide.HasFinished);
    }

    [Fact]
    public async Task Back_goes_back_a_step_and_on_the_first_step_skips()
    {
        var back = new BackNavigator(Services.GetRequiredService<NavigationManager>(), new Fakes.RecordingBack());
        var overlay = RenderComponent<EleGuideOverlay>(p => p.AddCascadingValue(back));
        await overlay.InvokeAsync(_guide.Start);
        overlay.Find(".guide-next").Click();

        await overlay.InvokeAsync(back.GoBackAsync);
        Assert.Equal(0, _guide.Index);
        Assert.True(_guide.IsOpen);

        await overlay.InvokeAsync(back.GoBackAsync);
        Assert.False(_guide.IsOpen);
    }

    [Fact]
    public void The_steps_tab_is_lit_above_the_dimmed_page()
    {
        var layout = RenderComponent<MainLayout>(p =>
            p.Add(x => x.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<p>page</p>"))));

        layout.InvokeAsync(_guide.Start);
        layout.InvokeAsync(_guide.Next);

        var shell = layout.Find(".shell");
        Assert.Contains("guiding", shell.ClassList);
        Assert.Contains("spot-containers", shell.ClassList);

        layout.InvokeAsync(_guide.Skip);
        Assert.DoesNotContain("guiding", layout.Find(".shell").ClassList);
    }
}
