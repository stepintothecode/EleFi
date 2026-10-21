using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Ui.Components;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components;

/// <summary>Ele in the middle of a screen, saying the screen's news, with the way on under it.</summary>
public class EleCentreTests : Bunit.TestContext
{
    public EleCentreTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddTransient<IMascotService, Fakes.SilentMascot>();
    }

    [Fact]
    public void Ele_says_the_title_and_the_rest_in_a_bubble_with_the_actions_under_it()
    {
        var centre = RenderComponent<EleCentre>(p => p
            .Add(c => c.Mood, MascotMood.Comforting)
            .Add(c => c.Title, "Sorry, this screen did not load.")
            .Add(c => c.ChildContent, (RenderFragment)(b => b.AddContent(0, "Nothing was lost.")))
            .Add(c => c.Actions, (RenderFragment)(b => b.AddMarkupContent(0, "<button>Try again</button>"))));

        Assert.Equal("Sorry, this screen did not load.", centre.Find(".ele-centre-title").TextContent);
        Assert.Equal("Nothing was lost.", centre.Find(".ele-centre-line").TextContent);
        Assert.Equal("Try again", centre.Find(".ele-centre-actions button").TextContent);
        Assert.Equal("Mascot, here for you", centre.Find("canvas").GetAttribute("aria-label"));
    }

    [Fact]
    public void With_nothing_to_do_there_are_no_buttons()
    {
        var centre = RenderComponent<EleCentre>(p => p.Add(c => c.Title, "Nothing deleted."));

        Assert.Empty(centre.FindAll(".ele-centre-actions"));
    }
}
