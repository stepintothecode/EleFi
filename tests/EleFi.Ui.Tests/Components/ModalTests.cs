using Bunit;
using Bunit.TestDoubles;
using EleFi.Ui.Components;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components;

/// <summary>
/// The bottom sheet that replaced "a form appended below a long list".
/// </summary>
public class ModalTests : Bunit.TestContext
{
    [Fact]
    public void A_closed_sheet_renders_nothing_at_all()
    {
        var component = RenderComponent<Modal>(p => p
            .Add(m => m.Open, false)
            .Add(m => m.Title, "New label"));

        // Not merely hidden. A sheet left in the DOM keeps its backdrop over the page and
        // its contents reachable by the screen reader.
        Assert.Equal(string.Empty, component.Markup.Trim());
    }

    [Fact]
    public void An_open_sheet_is_a_dialog_named_by_its_title()
    {
        var component = RenderComponent<Modal>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Title, "Delete this label?"));

        var dialog = component.Find("[role=dialog]");

        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        Assert.Equal("Delete this label?", dialog.GetAttribute("aria-label"));
    }

    [Fact]
    public void Both_the_backdrop_and_the_cross_dismiss_it()
    {
        var dismissals = 0;

        var component = RenderComponent<Modal>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Title, "New label")
            .Add(m => m.OnCancel, () => dismissals++));

        component.Find(".modal-backdrop").Click();
        component.Find("[aria-label=Close]").Click();

        // Two ways out, because a sheet with only one is a trap on a phone where the
        // obvious gesture is to tap the dimmed area.
        Assert.Equal(2, dismissals);
    }

    [Fact]
    public async Task The_system_back_gesture_closes_an_open_sheet_instead_of_leaving_the_screen()
    {
        var system = new Fakes.RecordingBack();
        var back = new BackNavigator(Services.GetRequiredService<FakeNavigationManager>(), system);
        back.Start();
        var dismissals = 0;

        RenderComponent<CascadingValue<BackNavigator>>(p => p
            .Add(c => c.Value, back)
            .AddChildContent<Modal>(m => m
                .Add(x => x.Open, true)
                .Add(x => x.Title, "New label")
                .Add(x => x.OnCancel, () => dismissals++)));

        Assert.True(await back.GoBackAsync());

        Assert.Equal(1, dismissals);
        Assert.Equal(0, system.Left);
    }

    [Fact]
    public async Task A_sheet_whose_parent_keeps_it_open_still_catches_the_next_back_press()
    {
        var system = new Fakes.RecordingBack();
        var back = new BackNavigator(Services.GetRequiredService<FakeNavigationManager>(), system);
        back.Start();
        var dismissals = 0;

        // The parent ignores the request: Open stays true.
        RenderComponent<CascadingValue<BackNavigator>>(p => p
            .Add(c => c.Value, back)
            .AddChildContent<Modal>(m => m
                .Add(x => x.Open, true)
                .Add(x => x.Title, "Saving")
                .Add(x => x.OnCancel, () => dismissals++)));

        await back.GoBackAsync();
        await back.GoBackAsync();

        Assert.Equal(2, dismissals);
        Assert.Equal(0, system.Left);
    }

    [Fact]
    public async Task A_closed_sheet_no_longer_catches_the_back_gesture()
    {
        var system = new Fakes.RecordingBack();
        var back = new BackNavigator(Services.GetRequiredService<FakeNavigationManager>(), system);
        back.Start();
        var dismissals = 0;

        var host = RenderComponent<CascadingValue<BackNavigator>>(p => p
            .Add(c => c.Value, back)
            .AddChildContent<Modal>(m => m
                .Add(x => x.Open, true)
                .Add(x => x.Title, "New label")
                .Add(x => x.OnCancel, () => dismissals++)));

        // Closed by its own cross, so the parent flips Open to false.
        host.FindComponent<Modal>().SetParametersAndRender(m => m.Add(x => x.Open, false));

        await back.GoBackAsync();

        // Back now does what it would on the dashboard, rather than "closing" a sheet that
        // is not there and appearing to do nothing.
        Assert.Equal(0, dismissals);
        Assert.Equal(1, system.Left);
    }
}
