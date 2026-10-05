using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Labels;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>The Labels page, reached from Settings' "Edit labels" button.</summary>
public class LabelSettingsTests : Bunit.TestContext
{
    [Fact]
    public void Every_label_is_listed()
    {
        Register();

        var component = RenderComponent<LabelSettings>();

        Assert.NotNull(component.Find("button[aria-label='Rename Food']"));
        Assert.NotNull(component.Find("button[aria-label='Rename Travel']"));
    }

    [Fact]
    public void Creating_and_renaming_a_label_happen_in_a_sheet_not_inline()
    {
        Register();

        var component = RenderComponent<LabelSettings>();

        Assert.Empty(component.FindAll("[role=dialog]"));

        component.Find(".section-head button").Click();

        Assert.Equal("New label", component.Find("[role=dialog]").GetAttribute("aria-label"));
    }

    [Fact]
    public void Deleting_a_label_asks_first_and_says_what_it_will_touch()
    {
        Register();

        var component = RenderComponent<LabelSettings>();
        component.Find("button[aria-label='Delete Food']").Click();

        var dialog = component.Find("[role=dialog]");

        Assert.Equal("Delete this label?", dialog.GetAttribute("aria-label"));
        Assert.Contains("stay exactly as they are", dialog.TextContent, StringComparison.Ordinal);
    }

    private void Register()
    {
        var clock = new Fakes.StoppedClock();
        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton(new LabelService(new Fakes.SeededLabels(), clock));
        Services.AddSingleton(new ToastService());
    }
}
