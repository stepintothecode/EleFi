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

        component.Find(".section-head button.add-label").Click();

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

    [Fact]
    public void A_deleted_label_comes_back_with_the_undo_arrow()
    {
        Register();
        var component = RenderComponent<LabelSettings>();

        component.Find("button[aria-label='Delete Food']").Click();
        component.Find("[role=dialog] button.danger").Click();
        component.WaitForAssertion(() => Assert.Empty(component.FindAll("button[aria-label='Rename Food']")));
        Assert.Equal(EleTone.Calm, Services.GetRequiredService<EleReactions>().Current!.Tone);

        component.Find(".section-head button.undo").Click();

        component.WaitForAssertion(() => Assert.NotNull(component.Find("button[aria-label='Rename Food']")));
    }

    private void Register()
    {
        var clock = new Fakes.StoppedClock();
        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton(new LabelService(new Fakes.SeededLabels(), clock));
        Services.AddEleAndUndo();
    }
}
