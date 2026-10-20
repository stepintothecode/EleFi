using Bunit.TestDoubles;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Services;

/// <summary>
/// What the system back gesture does: close what is open, step back to the screen before,
/// and close the app only on a second press when there is nothing left to go back to.
/// </summary>
public class BackNavigatorTests : Bunit.TestContext
{
    private readonly Fakes.RecordingBack _system = new();
    private readonly ManualTime _time = new();
    private readonly EleReactions _ele;

    public BackNavigatorTests() => _ele = new EleReactions(new Fakes.MemorySettings(), _time);

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
    public async Task One_back_press_never_closes_the_app_ele_asks_for_a_second()
    {
        var (_, back) = Start();

        Assert.True(await back.GoBackAsync());

        Assert.Equal(0, _system.Left);
        Assert.Equal(BackNavigator.ExitPrompt, _ele.Current!.Line);
    }

    [Fact]
    public async Task A_second_press_soon_after_closes_the_app()
    {
        var (_, back) = Start();

        await back.GoBackAsync();
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.False(await back.GoBackAsync());
        Assert.Equal(1, _system.Left);
    }

    [Fact]
    public async Task A_second_press_too_late_asks_again_instead_of_closing()
    {
        var (_, back) = Start();

        await back.GoBackAsync();
        _time.Advance(BackNavigator.ExitWindow + TimeSpan.FromSeconds(1));

        Assert.True(await back.GoBackAsync());
        Assert.Equal(0, _system.Left);
    }

    [Fact]
    public async Task Moving_somewhere_between_the_two_presses_cancels_the_close()
    {
        var (nav, back) = Start();

        await back.GoBackAsync();
        nav.NavigateTo("containers");
        await back.GoBackAsync();

        // Back from containers went home; it was a step, not the second "close" press.
        Assert.Equal(nav.BaseUri, nav.Uri);
        Assert.Equal(0, _system.Left);
    }

    [Fact]
    public async Task Back_from_the_dashboard_returns_to_the_screen_before_it()
    {
        var (nav, back) = Start();

        nav.NavigateTo("containers");
        nav.NavigateTo("/");

        Assert.True(await back.GoBackAsync());

        Assert.EndsWith("/containers", nav.Uri, StringComparison.Ordinal);
        Assert.Equal(0, _system.Left);
    }

    [Fact]
    public async Task Back_from_an_open_transaction_returns_to_the_list()
    {
        var (nav, back) = Start();

        nav.NavigateTo("transactions");
        nav.NavigateTo("transactions/5d2c8f7a-1b2c-4d5e-8f90-123456789abc");

        Assert.True(await back.GoBackAsync());

        Assert.EndsWith("/transactions", nav.Uri, StringComparison.Ordinal);
        Assert.Equal(0, _system.Left);
    }

    [Fact]
    public async Task A_screen_that_ends_itself_is_not_returned_to()
    {
        var (nav, back) = Start();

        nav.NavigateTo("containers");
        nav.NavigateTo("quick");

        // Quick capture cancelled: it leaves, and back from where it left to does not reopen it.
        back.Leave("/");
        Assert.EndsWith("/containers", nav.Uri, StringComparison.Ordinal);

        await back.GoBackAsync();
        Assert.Equal(nav.BaseUri, nav.Uri);
        Assert.Equal([""], back.History);
    }

    [Fact]
    public void Leaving_with_nothing_behind_replaces_the_screen_with_the_fallback()
    {
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.NavigateTo("transactions/5d2c8f7a-1b2c-4d5e-8f90-123456789abc");
        var back = new BackNavigator(nav, _system, _ele, _time);
        back.Start();
        nav.NavigateTo("transactions/5d2c8f7a-1b2c-4d5e-8f90-123456789abc");

        back.Leave("transactions");
        back.Leave("transactions");

        Assert.DoesNotContain("transactions/5d2c8f7a-1b2c-4d5e-8f90-123456789abc", back.History);
    }

    [Fact]
    public void Replacing_a_screen_adds_no_step()
    {
        var (nav, back) = Start();

        nav.NavigateTo("containers");
        back.ReplaceTo("capture");
        back.ReplaceTo("transactions");

        Assert.Equal(["", "transactions"], back.History);
    }

    [Fact]
    public async Task A_screen_opened_cold_steps_back_to_the_dashboard()
    {
        // A launch from the widget lands straight on quick capture, with nothing behind it.
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.NavigateTo("quick");

        var back = new BackNavigator(nav, _system, _ele, _time);
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
        Assert.Equal(BackNavigator.ExitPrompt, _ele.Current!.Line);
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
        var back = new BackNavigator(nav, _system, _ele, _time);
        back.Start();
        return (nav, back);
    }
}
