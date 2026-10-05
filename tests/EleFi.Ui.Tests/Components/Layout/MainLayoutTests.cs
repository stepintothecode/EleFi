using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Ui.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components.Layout;

/// <summary>
/// The layout every page renders inside.
/// </summary>
/// <remarks>
/// <para>
/// This exists because <c>HomeTests</c> did not catch a startup failure that stopped the
/// app opening at all. Those tests call <c>RenderComponent&lt;Home&gt;()</c>, which renders
/// the page <em>directly</em> and skips the router, the layout, and the nav. Anything that
/// throws in the shell is invisible to them, and the shell is what renders first.
/// </para>
/// <para>
/// The lesson generalises: testing a page in isolation says nothing about whether the app
/// can start.
/// </para>
/// </remarks>
public class MainLayoutTests : Bunit.TestContext
{
    private readonly EleFi.Ui.Services.PrivacyMode _privacy = new(new EleFi.Ui.Tests.Support.Fakes.MemorySettings());

    /// <summary>The shell hosts the toast list and the privacy switch, so it needs both.</summary>
    public MainLayoutTests()
    {
        Services.AddSingleton<EleFi.Ui.Services.ToastService>();
        Services.AddSingleton(_privacy);
    }

    [Fact]
    public void Hiding_amounts_marks_the_whole_shell_and_showing_them_unmarks_it()
    {
        var component = RenderComponent<MainLayout>(p =>
            p.Add(x => x.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<p class=\"amount\">₹450</p>"))));

        Assert.DoesNotContain("amounts-hidden", component.Find(".shell").ClassName, StringComparison.Ordinal);

        _privacy.Toggle();
        component.WaitForAssertion(() =>
            Assert.Contains("amounts-hidden", component.Find(".shell").ClassName, StringComparison.Ordinal));

        _privacy.Toggle();
        component.WaitForAssertion(() =>
            Assert.DoesNotContain("amounts-hidden", component.Find(".shell").ClassName, StringComparison.Ordinal));
    }

    [Fact]
    public void The_shell_renders_its_body()
    {
        var component = RenderComponent<MainLayout>(p =>
            p.Add(x => x.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<p>page content</p>"))));

        Assert.Contains("page content", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_shell_renders_the_tab_bar()
    {
        var component = RenderComponent<MainLayout>(p =>
            p.Add(x => x.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<p>x</p>"))));

        // If the layout throws, there is no navigation and the app is unusable even when
        // individual pages would have rendered.
        Assert.Contains("nav", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Capture", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_that_throws_is_contained_rather_than_taking_down_the_app()
    {
        var component = RenderComponent<MainLayout>(p =>
            p.Add(x => x.Body, (RenderFragment)(_ => throw new InvalidOperationException("boom"))));

        // The whole point of the boundary: the tabs survive, so there is a way out.
        Assert.Contains("This screen did not load", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Capture", component.Markup, StringComparison.Ordinal);
    }
}
