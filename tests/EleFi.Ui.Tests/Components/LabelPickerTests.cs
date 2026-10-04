using Bunit;
using EleFi.Domain.Labels;
using EleFi.Ui.Components;

namespace EleFi.Ui.Tests.Components;

/// <summary>
/// Multi-select label chips, the UI half of ADR-0013.
/// </summary>
public class LabelPickerTests : Bunit.TestContext
{
    private static readonly Label Food = new() { Name = "Food", Colour = "#f97316" };
    private static readonly Label Travel = new() { Name = "Travel", Colour = "#0ea5e9" };

    [Fact]
    public void Tapping_a_second_chip_adds_to_the_selection_rather_than_replacing_it()
    {
        IReadOnlyList<Guid> selected = [Food.Id];

        var component = RenderComponent<LabelPicker>(p => p
            .Add(x => x.Labels, [Food, Travel])
            .Add(x => x.Selected, selected)
            .Add(x => x.SelectedChanged, ids => selected = ids));

        component.FindAll("button")[1].Click();

        // The whole point of the change: a transaction is Food *and* Travel, not the more
        // recent of the two.
        Assert.Equal(2, selected.Count);
        Assert.Contains(Food.Id, selected);
        Assert.Contains(Travel.Id, selected);
    }

    [Fact]
    public void Tapping_a_chip_that_is_on_turns_it_off()
    {
        IReadOnlyList<Guid> selected = [Food.Id, Travel.Id];

        var component = RenderComponent<LabelPicker>(p => p
            .Add(x => x.Labels, [Food, Travel])
            .Add(x => x.Selected, selected)
            .Add(x => x.SelectedChanged, ids => selected = ids));

        component.FindAll("button")[0].Click();

        Assert.Equal([Travel.Id], selected);
    }

    [Fact]
    public void Selecting_nothing_is_a_legal_answer_and_needs_no_placeholder_label()
    {
        IReadOnlyList<Guid> selected = [Food.Id];

        var component = RenderComponent<LabelPicker>(p => p
            .Add(x => x.Labels, [Food, Travel])
            .Add(x => x.Selected, selected)
            .Add(x => x.SelectedChanged, ids => selected = ids));

        component.FindAll("button")[0].Click();

        // Unlabelled is a state, not an Uncategorised row. Nothing is substituted in.
        Assert.Empty(selected);
    }

    [Fact]
    public void Each_chip_reports_its_own_pressed_state_to_a_screen_reader()
    {
        var component = RenderComponent<LabelPicker>(p => p
            .Add(x => x.Labels, [Food, Travel])
            .Add(x => x.Selected, new[] { Food.Id }));

        var chips = component.FindAll("button");

        // Colour is doing the visual work here, so it cannot be the only carrier of meaning
        // (NFR-6.3).
        Assert.Equal("true", chips[0].GetAttribute("aria-pressed"));
        Assert.Equal("false", chips[1].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Labels_sit_in_one_sideways_row_in_the_order_given()
    {
        var component = RenderComponent<LabelPicker>(p => p.Add(x => x.Labels, [Travel, Food]));

        // A wrapping block of a dozen chips pushed the rest of the form off screen. The
        // caller passes them most used first, so that order must survive.
        Assert.Contains("scroller", component.Find(".label-picker").ClassName, StringComparison.Ordinal);
        Assert.Equal(["Travel", "Food"], component.FindAll("button").Select(b => b.TextContent.Trim()));
    }

    [Fact]
    public void With_no_labels_at_all_it_says_where_to_make_one()
    {
        var component = RenderComponent<LabelPicker>(p => p.Add(x => x.Labels, []));

        // Every seeded label is deletable now, so an empty list is reachable and cannot be
        // left as a blank gap in the form.
        Assert.Contains("Settings", component.Markup, StringComparison.Ordinal);
    }
}
