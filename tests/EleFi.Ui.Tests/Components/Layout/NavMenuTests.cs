using Bunit;
using EleFi.Ui.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components.Layout;

/// <summary>The bottom tabs, and the one pill that glides to the open tab.</summary>
public class NavMenuTests : Bunit.TestContext
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("plans?tab=goals", 1)]
    [InlineData("containers", 2)]
    [InlineData("quick", 3)]
    [InlineData("capture?amount=450", 4)]
    [InlineData("transactions", 5)]
    [InlineData("transactions/6f1c7e8a-1b1a-4f43-9a52-0d1d6b1e9a11", 5)]
    [InlineData("settings/backup", 6)]
    [InlineData("receipt", 4)]
    [InlineData("receipt?for=6f1c7e8a-1b1a-4f43-9a52-0d1d6b1e9a11&from=gallery", 5)]
    public void Each_page_belongs_to_the_tab_that_lights_up_for_it(string path, int tab) =>
        Assert.Equal(tab, NavMenu.TabFor(path));

    [Theory]
    [InlineData("notifications")]
    [InlineData("about")]
    [InlineData("contacts/6f1c7e8a-1b1a-4f43-9a52-0d1d6b1e9a11")]
    public void A_page_under_no_tab_has_none(string path) => Assert.Null(NavMenu.TabFor(path));

    [Fact]
    public void The_pill_moves_to_the_tab_opened_and_steps_aside_on_quick()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var menu = RenderComponent<NavMenu>();
        Assert.Equal("--tab:0", menu.Find(".tab-pill").GetAttribute("style"));

        nav.NavigateTo("transactions");
        menu.WaitForAssertion(() => Assert.Equal("--tab:5", menu.Find(".tab-pill").GetAttribute("style")));
        Assert.DoesNotContain("away", menu.Find(".tab-pill").ClassName, StringComparison.Ordinal);

        // Quick has its own disc: the pill fades where it was, and glides on from there later.
        nav.NavigateTo("quick");
        menu.WaitForAssertion(() => Assert.Contains("away", menu.Find(".tab-pill").ClassName, StringComparison.Ordinal));
        Assert.Equal("--tab:5", menu.Find(".tab-pill").GetAttribute("style"));

        nav.NavigateTo("plans");
        menu.WaitForAssertion(() => Assert.Equal("--tab:1", menu.Find(".tab-pill").GetAttribute("style")));
        Assert.DoesNotContain("away", menu.Find(".tab-pill").ClassName, StringComparison.Ordinal);
    }
}
