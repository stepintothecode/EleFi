using Bunit;
using EleFi.Ui.Components;

namespace EleFi.Ui.Tests.Components;

/// <summary>The "i" that replaced permanent explanatory text under form fields.</summary>
public class InfoButtonTests : Bunit.TestContext
{
    [Fact]
    public void The_explanation_is_hidden_until_asked_for()
    {
        var component = RenderComponent<InfoButton>(p => p
            .Add(x => x.Topic, "Paid with")
            .AddChildContent("<p>The rail the money moved along.</p>"));

        Assert.DoesNotContain("rail the money", component.Markup, StringComparison.Ordinal);
        Assert.Equal("What is Paid with?", component.Find(".info-btn").GetAttribute("aria-label"));
    }

    [Fact]
    public void Tapping_it_opens_a_sheet_and_got_it_closes_it()
    {
        var component = RenderComponent<InfoButton>(p => p
            .Add(x => x.Topic, "Paid with")
            .AddChildContent("<p>The rail the money moved along.</p>"));

        component.Find(".info-btn").Click();

        var dialog = component.Find("[role=dialog]");
        Assert.Equal("Paid with", dialog.GetAttribute("aria-label"));
        Assert.Contains("rail the money", dialog.TextContent, StringComparison.Ordinal);

        component.Find(".modal-body button.primary").Click();
        Assert.Empty(component.FindAll("[role=dialog]"));
    }
}
