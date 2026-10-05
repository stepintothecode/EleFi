using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Containers;
using EleFi.Ui.Components;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components;

/// <summary>
/// The router, which is the first component the app renders and the one nothing else works
/// without.
/// </summary>
/// <remarks>
/// <para>
/// This is the test that was missing when the app shipped unable to open. Page tests render
/// a page directly and layout tests render the shell directly; neither touches
/// <see cref="Routes"/>. So a router that threw on its very first parameter set was
/// invisible to a suite of 89 passing tests, and the symptom on the device was a spinner
/// that never went away with a red bar underneath it.
/// </para>
/// <para>
/// What used to happen: <c>NotFoundPage</c> pointed at a component with no <c>@page</c>
/// directive. .NET 10's router requires a routable type there and throws
/// <c>InvalidOperationException</c> before rendering anything at all.
/// </para>
/// </remarks>
public class RoutesTests : Bunit.TestContext
{
    [Fact]
    public void The_router_renders_the_home_route()
    {
        RegisterPageServices();

        var component = RenderComponent<Routes>();

        // Rendering at all is the assertion. This threw during OnParametersSet before the
        // NotFound page was given a route.
        Assert.Contains("Net worth", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_not_found_page_is_routable_so_the_router_will_accept_it()
    {
        // The router validates this eagerly, so an unroutable page here breaks every route,
        // not just the missing ones.
        var route = typeof(EleFi.Ui.Components.Pages.NotFound)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true);

        Assert.NotEmpty(route);
    }

    [Fact]
    public void Every_tab_destination_has_a_page_that_claims_it()
    {
        var routes = typeof(Routes).Assembly
            .GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>())
            .Select(r => r.Template.TrimStart('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // The tab bar hard-codes these. A typo in one is a dead tab, which is the kind of
        // thing nobody notices until they tap it.
        foreach (var tab in new[] { "", "containers", "quick", "capture", "transactions", "settings", "about" })
        {
            Assert.Contains(tab, routes, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_transaction_row_links_to_a_route_that_exists()
    {
        var routes = typeof(Routes).Assembly
            .GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .ToList();

        // The list builds this href by hand, so a mismatch is a row that opens nothing.
        Assert.Contains(routes, r => r.Contains("/transactions/{", StringComparison.Ordinal));
    }

    [Fact]
    public void A_launch_that_asked_for_a_screen_opens_on_it()
    {
        RegisterPageServices(new Fakes.PlainLaunch("about"));

        RenderComponent<Routes>();

        // A Suggestion Prompt or the widget asks for a screen; the dashboard would be wrong.
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/about", nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void A_request_while_open_navigates_once_and_is_then_forgotten()
    {
        var launch = new Fakes.PlainLaunch();
        RegisterPageServices(launch);
        var component = RenderComponent<Routes>();
        var nav = Services.GetRequiredService<NavigationManager>();

        launch.Request("about");
        component.WaitForAssertion(() => Assert.EndsWith("/about", nav.Uri, StringComparison.Ordinal));

        Assert.Null(launch.ConsumeRequestedRoute());
    }

    [Fact]
    public void The_system_back_gesture_returns_to_the_previous_screen()
    {
        var back = new Fakes.RecordingBack();
        RegisterPageServices(back: back);
        var component = RenderComponent<Routes>();
        var nav = Services.GetRequiredService<NavigationManager>();

        nav.NavigateTo("about");
        back.Press();

        // It used to close the whole app from any screen.
        component.WaitForAssertion(() => Assert.Equal(nav.BaseUri, nav.Uri));
        Assert.Equal(0, back.Left);
    }

    [Fact]
    public void The_system_back_gesture_on_the_dashboard_leaves_the_app()
    {
        var back = new Fakes.RecordingBack();
        RegisterPageServices(back: back);
        var component = RenderComponent<Routes>();

        back.Press();

        component.WaitForAssertion(() => Assert.Equal(1, back.Left));
    }

    private void RegisterPageServices(Fakes.PlainLaunch? launch = null, Fakes.RecordingBack? back = null)
    {
        var clock = new Fakes.StoppedClock();
        var containers = new Fakes.EmptyContainers();
        var transactions = new Fakes.EmptyTransactions();
        var systemBack = back ?? new Fakes.RecordingBack();

        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton<IContainerRepository>(containers);
        Services.AddSingleton<ITransactionRepository>(transactions);
        Services.AddSingleton(new ContainerService(containers, transactions, clock));
        Services.AddSingleton<IMascotService>(new Fakes.SilentMascot());
        Services.AddSingleton<ILaunchIntent>(launch ?? new Fakes.PlainLaunch());
        Services.AddSingleton<ISystemBack>(systemBack);
        Services.AddSingleton(sp => new BackNavigator(sp.GetRequiredService<NavigationManager>(), systemBack));
        Services.AddSingleton<ToastService>();
        Services.AddSingleton<ILinkOpener>(new NoLinks());
        Services.AddSingleton(new PrivacyMode(new Fakes.MemorySettings()));
        Services.AddSingleton(new TransactionListState());
    }

    private sealed class NoLinks : ILinkOpener
    {
        public Task OpenAsync(string url, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
