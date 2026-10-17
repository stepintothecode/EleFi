using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;

namespace EleFi.Ui.Tests.Services;

/// <summary>Ele's guide: its order, moving through it, and when it starts on its own.</summary>
public class EleGuideTests
{
    private readonly Fakes.MemorySettings _settings = new();

    [Fact]
    public void Containers_come_straight_after_the_hello_because_nothing_works_without_one()
    {
        Assert.Equal(GuideSpot.None, EleGuide.Steps[0].Spot);
        Assert.Equal(GuideSpot.Containers, EleGuide.Steps[1].Spot);
    }

    [Fact]
    public void Every_major_feature_has_a_step()
    {
        var spots = EleGuide.Steps.Select(s => s.Spot).ToHashSet();

        foreach (var tab in Enum.GetValues<GuideSpot>().Where(s => s != GuideSpot.None))
        {
            Assert.Contains(tab, spots);
        }
    }

    [Fact]
    public void Every_step_is_short()
    {
        // Minimal words: a few for the title and one sentence for the line.
        foreach (var step in EleGuide.Steps)
        {
            Assert.True(step.Title.Split(' ').Length <= 5, step.Title);
            Assert.True(step.Line.Split(' ').Length <= 16, step.Line);
        }
    }

    [Fact]
    public void Each_sentence_of_a_step_is_its_own_line()
    {
        Assert.Equal(
            ["Your money at a glance.", "Tap me on the home page to hide amounts."],
            EleGuide.Sentences("Your money at a glance. Tap me on the home page to hide amounts."));
    }

    [Fact]
    public void The_last_step_says_where_to_tap_and_does_not_point_at_settings()
    {
        var last = EleGuide.Steps[^1].Line;

        Assert.Contains("Tap me on the home page to hide amounts", last, StringComparison.Ordinal);
        Assert.DoesNotContain("Settings", last, StringComparison.Ordinal);
    }

    [Fact]
    public void Next_and_previous_move_one_step_and_previous_stops_at_the_first()
    {
        var guide = new EleGuide(_settings);
        guide.Start();

        guide.Previous();
        Assert.Equal(0, guide.Index);

        guide.Next();
        guide.Next();
        Assert.Equal(2, guide.Index);

        guide.Previous();
        Assert.Equal(1, guide.Index);
    }

    [Fact]
    public void Next_on_the_last_step_finishes_and_remembers()
    {
        var guide = new EleGuide(_settings);
        guide.Start();

        for (var i = 0; i < EleGuide.Steps.Count; i++)
        {
            guide.Next();
        }

        Assert.False(guide.IsOpen);
        Assert.True(guide.HasFinished);
    }

    [Fact]
    public void Skip_ends_it_from_any_step_and_it_never_starts_on_its_own_again()
    {
        var guide = new EleGuide(_settings);
        guide.Start();
        guide.Next();

        guide.Skip();

        Assert.False(guide.IsOpen);
        Assert.False(new EleGuide(_settings).ShouldStartOnItsOwn(hasContainers: false));
    }

    [Fact]
    public void It_starts_on_its_own_only_for_a_new_user_with_no_containers()
    {
        var guide = new EleGuide(_settings);

        Assert.True(guide.ShouldStartOnItsOwn(hasContainers: false));
        Assert.False(guide.ShouldStartOnItsOwn(hasContainers: true));
    }

    [Fact]
    public void Starting_from_settings_works_even_after_it_was_finished()
    {
        var guide = new EleGuide(_settings);
        guide.Skip();

        guide.Start();

        Assert.True(guide.IsOpen);
        Assert.Equal(0, guide.Index);
    }

    [Fact]
    public void Moving_says_so_so_the_overlay_can_redraw()
    {
        var guide = new EleGuide(_settings);
        var changes = 0;
        guide.Changed += () => changes++;

        guide.Start();
        guide.Next();
        guide.Previous();
        guide.Skip();

        Assert.Equal(4, changes);
    }
}
