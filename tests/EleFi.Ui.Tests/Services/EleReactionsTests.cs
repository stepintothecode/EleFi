using EleFi.Application.Abstractions;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;

namespace EleFi.Ui.Tests.Services;

/// <summary>
/// Ele is the only way the app tells the user what just happened: it pops up, says it, and
/// goes away by itself within a few seconds, or at once when told to.
/// </summary>
public class EleReactionsTests
{
    private readonly ManualTime _time = new();
    private readonly Fakes.MemorySettings _settings = new();

    [Fact]
    public void Recording_brings_ele_on_pleased_with_something_to_say_about_it()
    {
        var ele = New();

        ele.React(EleMoment.Recorded, "₹450.00", "Zomato");

        var reaction = Assert.IsType<EleReaction>(ele.Current);
        Assert.Equal(EleTone.Pleased, reaction.Tone);
        Assert.Equal(MascotMood.Happy, reaction.Mood);
        Assert.False(string.IsNullOrWhiteSpace(reaction.Line));
        Assert.Equal("₹450.00", reaction.Amount);
        Assert.Equal("Zomato", reaction.About);
    }

    [Fact]
    public void A_deletion_is_met_calmly_neither_pleased_nor_cross()
    {
        var ele = New();

        ele.React(EleMoment.Deleted);

        Assert.Equal(EleTone.Calm, ele.Current!.Tone);
        Assert.Equal(MascotMood.Idle, ele.Current.Mood);
    }

    [Fact]
    public void A_problem_brings_ele_on_cross_saying_exactly_what_is_wrong()
    {
        var ele = New();

        ele.Problem("Who was it paid to?");

        Assert.Equal(EleTone.Cross, ele.Current!.Tone);
        Assert.Equal(MascotMood.Concerned, ele.Current.Mood);
        Assert.Equal("Who was it paid to?", ele.Current.Line);
        Assert.Null(ele.Current.Moment);
    }

    [Fact]
    public void Anything_can_be_said_in_any_tone()
    {
        var ele = New();

        ele.Say("Linked Rahul and Rahul SBI.", EleTone.Pleased, about: "Contacts");

        Assert.Equal("Linked Rahul and Rahul SBI.", ele.Current!.Line);
        Assert.Equal("Contacts", ele.Current.About);
    }

    [Fact]
    public void Ele_leaves_by_itself_within_three_to_five_seconds()
    {
        var ele = New();
        Assert.InRange(EleReactions.Lifetime, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));

        ele.React(EleMoment.Updated);
        _time.Advance(EleReactions.Lifetime - TimeSpan.FromMilliseconds(1));
        Assert.NotNull(ele.Current);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Null(ele.Current);
    }

    [Fact]
    public void A_problem_stays_a_little_longer_so_it_can_be_read_but_not_past_five_seconds()
    {
        var ele = New();

        ele.Problem("Enter an amount greater than zero.");
        _time.Advance(EleReactions.Lifetime);

        Assert.NotNull(ele.Current);
        _time.Advance(EleReactions.ProblemLifetime - EleReactions.Lifetime);
        Assert.Null(ele.Current);
        Assert.True(EleReactions.ProblemLifetime <= TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void The_cross_sends_ele_away_at_once_and_the_old_timer_brings_nothing_back()
    {
        var ele = New();

        ele.React(EleMoment.Recorded);
        ele.Dismiss();
        _time.Advance(EleReactions.Lifetime);

        Assert.Null(ele.Current);
    }

    [Fact]
    public void Leaving_on_its_own_or_by_the_cross_plays_the_same_fade_and_then_ends()
    {
        var ele = New();

        ele.React(EleMoment.Recorded);
        var shown = ele.Current!;
        _time.Advance(EleReactions.Lifetime);

        // Expired, but still on screen for its fade, so it does not just blink out.
        Assert.Null(ele.Current);
        Assert.Same(shown, ele.Leaving);

        _time.Advance(EleReactions.LeaveDuration);
        Assert.Null(ele.Leaving);

        ele.Problem("Enter an amount greater than zero.");
        var problem = ele.Current!;
        ele.Dismiss();
        Assert.Same(problem, ele.Leaving);
    }

    [Fact]
    public void A_second_reaction_takes_the_place_while_the_first_slides_down_and_fades()
    {
        var ele = New();

        ele.React(EleMoment.Recorded);
        var first = ele.Current!;
        ele.React(EleMoment.Deleted);

        Assert.Equal(EleMoment.Deleted, ele.Current!.Moment);
        Assert.Same(first, ele.Leaving);

        // Gone once its fade has played.
        _time.Advance(EleReactions.LeaveDuration);
        Assert.Null(ele.Leaving);
        Assert.NotNull(ele.Current);
    }

    [Fact]
    public void A_second_reaction_gets_its_own_few_seconds()
    {
        var ele = New();

        ele.React(EleMoment.Recorded);
        _time.Advance(TimeSpan.FromSeconds(3));
        ele.React(EleMoment.Deleted);
        _time.Advance(TimeSpan.FromSeconds(2));

        // The first one's timer has passed; it must not take the second one with it.
        Assert.Equal(EleMoment.Deleted, ele.Current!.Moment);

        _time.Advance(EleReactions.Lifetime);
        Assert.Null(ele.Current);
    }

    [Fact]
    public void The_same_kind_of_change_twice_running_gets_a_different_line()
    {
        var ele = New();

        ele.React(EleMoment.Recorded);
        var first = ele.Current!.Line;
        ele.React(EleMoment.Recorded);

        Assert.NotEqual(first, ele.Current!.Line);
    }

    [Fact]
    public void Ele_never_says_saved_which_reads_like_money_put_aside()
    {
        foreach (var moment in Enum.GetValues<EleMoment>())
        {
            for (var i = 0; i < 6; i++)
            {
                Assert.DoesNotContain("saved", EleReactions.LineFor(moment, i), StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Sound_is_on_unless_switched_off_and_the_choice_is_kept()
    {
        var ele = New();
        Assert.True(ele.SoundEnabled);

        ele.SetSoundEnabled(false);

        Assert.False(New().SoundEnabled);
    }

    private EleReactions New() => new(_settings, _time);
}
