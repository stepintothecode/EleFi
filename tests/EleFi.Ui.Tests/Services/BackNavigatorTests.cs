using Bunit.TestDoubles;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Services;

/// <summary>
/// What the system back gesture does: close what is open, step back a screen, and leave the
/// app only from the dashboard.
/// </summary>
public class BackNavigatorTests : Bunit.TestContext
{
    private readonly Fakes.RecordingBack _system = new();

    [Fact]
    public async Task Back_returns_to_the_previous_screen_instead_of_closing_the_app()
    {
        var (nav, back) = Start();

        nav.NavigateTo("containers");
        nav.NavigateTo("quick");

        Assert.True(await back.GoBackAsync());

        Assert.EndsWith("/containers", nav.Uri, StringComparison.Ordinal);
        Assert.Equal(0, _system.Left);
    }

    [Fact]
    public async Task Back_from_a_page_inside_settings_returns_to_settings()
    {
        var (nav, back) = Start();

        nav.NavigateTo("settings");
        nav.NavigateTo("settings/labels");

        Assert.True(await back.GoBackAsync());

        Assert.EndsWith("/settings", nav.Uri, StringComparison.Ordinal);
        Assert.Equal(0, _system.Left);
    }

    [Fact]
    public async Task Stepping_back_twice_walks_the_history_in_reverse()
    {
        var (nav, back) = Start();

        nav.NavigateTo("containers");
        nav.NavigateTo("capture");
        nav.NavigateTo("quick");

        await back.GoBackAsync();
        await back.GoBackAsync();

        // The first back must not have been recorded as a new visit, or the second one
        // would bounce straight back to quick.
        Assert.EndsWith("/containers", nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Back_from_the_dashboard_leaves_the_app()
    {
        var (_, back) = Start();

        Assert.False(await back.GoBackAsync());
        Assert.Equal(1, _system.Left);
    }

    [Fact]
    public async Task Reaching_the_dashboard_starts_the_history_again()
    {
        var (nav, back) = Start();

        nav.NavigateTo("quick");

        // Quick capture's Cancel goes home. Back from there should leave, not reopen the
        // screen that was just cancelled.
        nav.NavigateTo("/");

        Assert.False(await back.GoBackAsync());
        Assert.Equal(1, _system.Left);
    }

    [Fact]
    public async Task A_screen_opened_cold_steps_back_to_the_dashboard()
    {
        // A launch from the widget lands straight on quick capture, with nothing behind it.
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.NavigateTo("quick");

        var back = new BackNavigator(nav, _system);
        back.Start();

        Assert.True(await back.GoBackAsync());
        Assert.Equal(nav.BaseUri, nav.Uri);
    }

    [Fact]
    public async Task An_open_sheet_closes_before_any_navigation_happens()
    {
        var (nav, back) = Start();
        nav.NavigateTo("containers");

        var closed = 0;
        using var sheet = back.Intercept(() =>
        {
            closed++;
            return Task.CompletedTask;
        });

        Assert.True(await back.GoBackAsync());

        Assert.Equal(1, closed);
        Assert.EndsWith("/containers", nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_most_recent_interception_wins()
    {
        var (_, back) = Start();
        var order = new List<string>();

        using var outer = back.Intercept(() => Record(order, "outer"));
        using var inner = back.Intercept(() => Record(order, "inner"));

        await back.GoBackAsync();
        await back.GoBackAsync();

        // A sheet opened over a sheet closes first. Each interception is used once.
        Assert.Equal(["inner", "outer"], order);
    }

    [Fact]
    public async Task A_released_interception_is_never_called()
    {
        var (_, back) = Start();
        var called = false;

        var sheet = back.Intercept(() =>
        {
            called = true;
            return Task.CompletedTask;
        });

        // The sheet was closed by its own cross, so back should no longer try to close it.
        sheet.Dispose();
        await back.GoBackAsync();

        Assert.False(called);
        Assert.Equal(1, _system.Left);
    }

    [Fact]
    public void Visiting_the_same_screen_twice_in_a_row_is_one_step()
    {
        var (nav, back) = Start();

        nav.NavigateTo("containers");
        nav.NavigateTo("containers");

        Assert.Equal(["", "containers"], back.History);
    }

    [Fact]
    public void The_history_does_not_grow_without_bound()
    {
        var (nav, back) = Start();

        for (var i = 0; i < 200; i++)
        {
            nav.NavigateTo(i % 2 == 0 ? "containers" : "capture");
        }

        Assert.True(back.History.Count <= BackNavigator.MaximumDepth);
    }

    private static Task Record(List<string> order, string name)
    {
        order.Add(name);
        return Task.CompletedTask;
    }

    private (FakeNavigationManager Nav, BackNavigator Back) Start()
    {
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        var back = new BackNavigator(nav, _system);
        back.Start();
        return (nav, back);
    }
}
