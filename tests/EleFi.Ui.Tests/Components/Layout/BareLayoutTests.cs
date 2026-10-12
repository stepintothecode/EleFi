using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Ui.Components.Layout;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components.Layout;

/// <summary>The tab-less layout quick capture opens in.</summary>
public class BareLayoutTests : Bunit.TestContext
{
    private readonly EleReactions _ele = new(new Fakes.MemorySettings(), new ManualTime());

    public BareLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new PrivacyMode(new Fakes.MemorySettings()));
        Services.AddSingleton(_ele);
        Services.AddEleAndUndo();
        Services.AddTransient<IMascotService, Fakes.SilentMascot>();
    }

    [Fact]
    public void Ele_reacts_to_a_quick_capture_too()
    {
        var component = RenderComponent<BareLayout>(p =>
            p.Add(x => x.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<p>quick</p>"))));

        _ele.React(EleMoment.Recorded, "₹450.00", "Zomato");

        component.WaitForAssertion(() => Assert.NotNull(component.Find(".ele-pop")));
        Assert.Empty(component.FindAll("nav"));
    }
}
