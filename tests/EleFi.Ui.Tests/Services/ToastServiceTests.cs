using Bunit;
using EleFi.Ui.Components;
using EleFi.Ui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Services;

/// <summary>Toasts, and the Undo button some of them carry.</summary>
public class ToastServiceTests : Bunit.TestContext
{
    [Fact]
    public void A_toast_with_an_action_shows_its_button_and_tapping_it_runs_once_and_dismisses()
    {
        var toasts = new ToastService();
        Services.AddSingleton(toasts);
        var host = RenderComponent<ToastHost>();
        var undone = 0;

        toasts.Ok("Saved.", new ToastAction("Undo", () =>
        {
            undone++;
            return Task.CompletedTask;
        }));

        host.WaitForAssertion(() => Assert.Equal("Undo", host.Find(".toast-action").TextContent));
        host.Find(".toast-action").Click();

        Assert.Equal(1, undone);
        Assert.Empty(toasts.Current);
    }

    [Fact]
    public void An_undo_toast_stays_longer_than_a_plain_one()
    {
        Assert.True(ToastService.ActionLifetime > ToastService.SuccessLifetime);
    }
}
