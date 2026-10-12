using EleFi.Domain.Streaks;

namespace EleFi.Domain.Tests.Streaks;

/// <summary>
/// The Steady ledger streak: weeks, not days, kept by leaving nothing to review and no plan
/// open from that week.
/// </summary>
public class SteadyLedgerTests
{
    // A Sunday. This week began on Monday 5 October.
    private static readonly DateOnly Today = new(2026, 10, 11);
    private static readonly DateOnly ThisMonday = new(2026, 10, 5);

    [Fact]
    public void Before_anything_is_recorded_there_is_no_streak()
    {
        var streak = SteadyLedger.Compute(null, [], [], Today);

        Assert.Equal(0, streak.Weeks);
        Assert.Empty(streak.Recent);
    }

    [Fact]
    public void Each_tidy_week_counts_and_this_week_counts_while_it_is_tidy()
    {
        var streak = SteadyLedger.Compute(ThisMonday.AddDays(-21), [], [], Today);

        Assert.Equal(4, streak.Weeks);
        Assert.Equal(4, streak.MilestoneReached);
        Assert.Equal(12, streak.NextMilestone);
        Assert.All(streak.Recent, w => Assert.Equal(WeekResult.Clean, w.Result));
    }

    [Fact]
    public void A_week_with_something_still_to_review_breaks_the_run()
    {
        var started = ThisMonday.AddDays(-21);

        var streak = SteadyLedger.Compute(started, needsReviewOn: [ThisMonday.AddDays(-12)], openPlansDueOn: [], Today);

        // Three weeks ago clean, two weeks ago missed, last week clean, this week clean.
        Assert.Equal(2, streak.Weeks);
        Assert.Equal(WeekResult.Missed, streak.Recent[^2].Result);
    }

    [Fact]
    public void A_plan_left_open_from_a_past_week_breaks_it_too()
    {
        var streak = SteadyLedger.Compute(ThisMonday.AddDays(-7), [], openPlansDueOn: [ThisMonday.AddDays(-3)], Today);

        Assert.Equal(1, streak.Weeks);
        Assert.Equal(WeekResult.Missed, streak.Recent[^1].Result);
    }

    [Fact]
    public void Four_clean_weeks_earn_a_freeze_that_saves_the_next_missed_one()
    {
        var started = ThisMonday.AddDays(-35);

        // Weeks 1 to 4 clean, week 5 (last week) has something to review.
        var streak = SteadyLedger.Compute(started, needsReviewOn: [ThisMonday.AddDays(-4)], openPlansDueOn: [], Today);

        Assert.Equal(WeekResult.Frozen, streak.Recent[^1].Result);
        Assert.Equal(5, streak.Weeks);
        Assert.Equal(0, streak.Freezes);
    }

    [Fact]
    public void This_week_untidy_so_far_does_not_break_the_streak_it_only_waits()
    {
        var streak = SteadyLedger.Compute(ThisMonday.AddDays(-14), needsReviewOn: [Today], openPlansDueOn: [Today], Today);

        Assert.Equal(2, streak.Weeks);
        Assert.Equal(1, streak.ThisWeek.ToReview);
        Assert.Equal(1, streak.ThisWeek.PlansOpen);
        Assert.False(streak.ThisWeek.IsTidy);
    }

    [Fact]
    public void A_plan_due_later_is_not_a_problem_yet()
    {
        var streak = SteadyLedger.Compute(ThisMonday, [], openPlansDueOn: [Today.AddDays(3)], Today);

        Assert.True(streak.ThisWeek.IsTidy);
        Assert.Equal(1, streak.Weeks);
    }

    [Fact]
    public void Only_the_last_eight_weeks_are_shown()
    {
        var streak = SteadyLedger.Compute(ThisMonday.AddDays(-7 * 20), [], [], Today);

        Assert.Equal(8, streak.Recent.Count);
        Assert.Equal(ThisMonday.AddDays(-7), streak.Recent[^1].Monday);
        Assert.Equal(21, streak.Weeks);
        Assert.Equal(SteadyLedger.MaximumFreezes, streak.Freezes);
    }
}
