using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Containers;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>
/// The dashboard on a phone that has just installed the app.
/// </summary>
/// <remarks>
/// This is the regression test for the first-launch crash. The net-worth tiles rendered on
/// the synchronous first pass, before <c>OnInitializedAsync</c> had run, and formatted a
/// <c>default(NetWorth)</c> whose currency had a null code. That threw
/// <c>ArgumentNullException</c> out of a dictionary lookup, Blazor caught it at the top of
/// the tree, and the whole app showed "An unhandled error has occurred" on a blank screen.
/// </remarks>
public class HomeTests : Bunit.TestContext
{
    [Fact]
    public void It_renders_on_a_completely_empty_database()
    {
        Register();

        var component = RenderComponent<Home>();

        // Rendering at all is the assertion. Before the fix this threw during the first
        // synchronous pass, so there was nothing to assert against.
        Assert.Contains("Net worth", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Liquid", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void It_shows_the_empty_state_that_points_at_the_next_action()
    {
        Register();

        var component = RenderComponent<Home>();

        // An empty state that explains nothing is a dead end on the one screen a new user
        // sees first.
        Assert.Contains("Add a container", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlapping_labels_are_explained_where_the_bars_are()
    {
        // 2,000 spent once, labelled both Food and Household. The bars add up to 4,000.
        Register(new SpendBreakdown(
            [
                new LabelSpend(Guid.NewGuid(), "Food", "#f97316", 2_000_00),
                new LabelSpend(Guid.NewGuid(), "Household", "#0ea5e9", 2_000_00),
            ],
            2_000_00));

        var component = RenderComponent<Home>();

        // Someone who adds the bars up and gets more than they spent is told why on the same
        // screen, rather than concluding the app cannot count.
        Assert.Contains("counted under each", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Labels_that_do_not_overlap_get_no_explanation()
    {
        // Two labels, but on different transactions, so the bars sum to the total exactly.
        Register(new SpendBreakdown(
            [
                new LabelSpend(Guid.NewGuid(), "Food", "#f97316", 1_200_00),
                new LabelSpend(Guid.NewGuid(), "Travel", "#0ea5e9", 800_00),
            ],
            2_000_00));

        var component = RenderComponent<Home>();

        // Explaining a discrepancy that is not on screen teaches the reader to distrust a
        // total that was right all along.
        Assert.DoesNotContain("counted under each", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Payments_waiting_to_be_confirmed_are_announced_and_link_to_the_inbox()
    {
        Register(toConfirm: 2);

        var component = RenderComponent<Home>();

        var banner = component.Find("a.inbox-banner");
        Assert.Equal("suggestions", banner.GetAttribute("href"));
        Assert.Contains("2 payments to confirm", banner.TextContent, StringComparison.Ordinal);

        // SM2: the dashboard says plainly that these are not in the figures beside them.
        Assert.Contains("Not counted yet", banner.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void With_nothing_waiting_there_is_no_banner()
    {
        Register();

        var component = RenderComponent<Home>();

        Assert.Empty(component.FindAll("a.inbox-banner"));
    }

    private void Register(SpendBreakdown? spend = null, int toConfirm = 0)
    {
        var clock = new Fakes.StoppedClock();
        var containers = new Fakes.EmptyContainers();
        var transactions = new Fakes.EmptyTransactions { Spend = spend ?? new SpendBreakdown([], 0) };

        var suggestions = Substitute.For<ISuggestionRepository>();
        suggestions.CountPendingAsync(Arg.Any<CancellationToken>()).Returns(toConfirm);
        Services.AddSingleton(suggestions);

        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton<ITransactionRepository>(transactions);
        Services.AddSingleton<IContainerRepository>(containers);
        Services.AddSingleton(new ContainerService(containers, transactions, clock));
        Services.AddSingleton<IMascotService>(new Fakes.SilentMascot());
    }
}
