using Bunit;
using EleFi.Application.Typeahead;
using EleFi.Ui.Components;

namespace EleFi.Ui.Tests.Components;

/// <summary>
/// The "Paid to" field: likely answers above it, the whole history searched as you type.
/// </summary>
public class TypeaheadInputTests : Bunit.TestContext
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<TypeaheadCandidate> History =
    [
        new("Zomato", 12, Now),
        new("Swiggy", 8, Now),
        new("Rahul", 5, Now),
        new("Landlord", 1, Now),
        new("Chaiwala", 0, Now.AddYears(-1)),
    ];

    [Fact]
    public void With_nothing_typed_the_three_most_used_sit_above_the_field()
    {
        var component = RenderComponent<TypeaheadInput>(p => p.Add(x => x.Candidates, History));

        Assert.Equal(["Zomato", "Swiggy", "Rahul"], Options(component));

        // Above, not below: the keyboard rises over anything drawn under the field.
        var children = component.Find(".typeahead").Children;
        Assert.Contains("typeahead-panel", children[0].ClassName, StringComparison.Ordinal);
        Assert.Equal("INPUT", children[1].TagName);
    }

    [Fact]
    public void Typing_searches_the_whole_history()
    {
        var component = RenderComponent<TypeaheadInput>(p => p.Add(x => x.Candidates, History));

        component.Find("input").Input("chai");

        Assert.Equal(["Chaiwala"], Options(component));
    }

    [Fact]
    public void Typing_reports_every_keystroke_to_the_form()
    {
        string? value = null;
        var component = RenderComponent<TypeaheadInput>(p => p
            .Add(x => x.Candidates, History)
            .Add(x => x.ValueChanged, v => value = v));

        component.Find("input").Input("Zo");

        Assert.Equal("Zo", value);
    }

    [Fact]
    public void Tapping_an_answer_fills_the_field_reports_the_pick_and_puts_the_panel_away()
    {
        string? value = null;
        string? picked = null;
        var component = RenderComponent<TypeaheadInput>(p => p
            .Add(x => x.Candidates, History)
            .Add(x => x.ValueChanged, v => value = v)
            .Add(x => x.OnPicked, v => picked = v));

        component.FindAll("[role=option]")[1].Click();

        Assert.Equal("Swiggy", value);
        Assert.Equal("Swiggy", picked);
        Assert.Equal("Swiggy", component.Find("input").GetAttribute("value"));

        // Shown once, in the field. Not again in a box above it.
        Assert.Empty(component.FindAll(".typeahead-panel"));
    }

    [Fact]
    public void Typing_a_name_exactly_does_not_offer_that_same_name_back()
    {
        var component = RenderComponent<TypeaheadInput>(p => p.Add(x => x.Candidates, History));

        component.Find("input").Input("Rahul");

        Assert.Empty(component.FindAll(".typeahead-panel"));
    }

    [Fact]
    public void Typing_part_of_a_name_offers_the_others_but_not_what_is_already_typed()
    {
        var component = RenderComponent<TypeaheadInput>(p => p.Add(x => x.Candidates,
            [new TypeaheadCandidate("Swiggy", 2, Now), new TypeaheadCandidate("Swiggy Instamart", 1, Now)]));

        component.Find("input").Input("swiggy");

        Assert.Equal(["Swiggy Instamart"], Options(component));
    }

    [Fact]
    public void A_name_nobody_has_used_says_it_will_be_added()
    {
        var component = RenderComponent<TypeaheadInput>(p => p.Add(x => x.Candidates, History));

        component.Find("input").Input("Brand new shop");

        Assert.Empty(component.FindAll("[role=option]"));
        Assert.Contains("will be added as new", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_panel_is_captioned_suggestions_whether_idle_or_typing()
    {
        var component = RenderComponent<TypeaheadInput>(p => p.Add(x => x.Candidates, History));

        Assert.Contains("Suggestions", component.Find(".typeahead-caption").TextContent, StringComparison.Ordinal);

        component.Find("input").Input("Zo");

        Assert.Contains("Suggestions", component.Find(".typeahead-caption").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Free_text_such_as_a_note_never_says_it_will_be_added_as_new()
    {
        var component = RenderComponent<TypeaheadInput>(p => p
            .Add(x => x.Candidates, History)
            .Add(x => x.OffersNew, false));

        component.Find("input").Input("Something nobody wrote before");

        Assert.Empty(component.FindAll(".typeahead-panel"));
    }

    [Fact]
    public void A_value_the_form_starts_with_shows_no_panel()
    {
        // Editing a transaction: the payee is already there, and repeating it as a "match"
        // above the field was noise.
        var component = RenderComponent<TypeaheadInput>(p => p
            .Add(x => x.Candidates, History)
            .Add(x => x.Value, "Zom"));

        Assert.Empty(component.FindAll(".typeahead-panel"));
    }

    [Fact]
    public void An_idle_count_of_zero_keeps_the_panel_away_until_typing_starts()
    {
        // The app fields are optional, so they show nothing until the user engages with them.
        var component = RenderComponent<TypeaheadInput>(p => p
            .Add(x => x.Candidates, History)
            .Add(x => x.IdleCount, 0));

        Assert.Empty(component.FindAll(".typeahead-panel"));

        component.Find("input").Input("swi");
        Assert.Equal(["Swiggy"], Options(component));
    }

    private static List<string> Options(IRenderedComponent<TypeaheadInput> component) =>
        component.FindAll("[role=option]").Select(o => o.TextContent.Trim()).ToList();
}
