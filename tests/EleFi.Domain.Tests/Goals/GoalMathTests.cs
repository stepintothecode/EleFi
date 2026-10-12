using EleFi.Domain.Goals;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Tests.Goals;

/// <summary>Progress, status and the monthly amount a goal still needs.</summary>
public class GoalMathTests
{
    // ₹3,00,000 over two years, from 1 Sep 2026 to 1 Sep 2028.
    private static readonly Goal Ring = new()
    {
        Name = "Ring",
        TargetAmountMinor = 3_00_000_00,
        StartDate = new DateOnly(2026, 9, 1),
        TargetDate = new DateOnly(2028, 9, 1),
    };

    [Fact]
    public void Halfway_through_the_time_half_the_target_is_expected()
    {
        var halfway = DateOnly.FromDayNumber((Ring.StartDate.DayNumber + Ring.TargetDate.DayNumber) / 2);

        Assert.InRange(GoalMath.ExpectedBy(Ring, halfway), 1_49_000_00, 1_51_000_00);
    }

    [Fact]
    public void Expected_is_nothing_before_the_start_and_everything_after_the_target_date()
    {
        Assert.Equal(0, GoalMath.ExpectedBy(Ring, new DateOnly(2026, 1, 1)));
        Assert.Equal(Ring.TargetAmountMinor, GoalMath.ExpectedBy(Ring, new DateOnly(2030, 1, 1)));
    }

    [Fact]
    public void Within_five_percent_of_the_line_is_on_track_and_below_it_is_behind()
    {
        var day = new DateOnly(2027, 9, 1);
        var expected = GoalMath.ExpectedBy(Ring, day);

        Assert.Equal(GoalStatus.OnTrack, GoalMath.Stand(Ring, expected * 96 / 100, day).Status);
        Assert.Equal(GoalStatus.Behind, GoalMath.Stand(Ring, expected * 90 / 100, day).Status);
    }

    [Fact]
    public void Reaching_the_target_is_achieved_and_needs_nothing_more()
    {
        var standing = GoalMath.Stand(Ring, 3_10_000_00, new DateOnly(2027, 1, 1));

        Assert.Equal(GoalStatus.Achieved, standing.Status);
        Assert.Equal(0, standing.RequiredMonthlyMinor);
        Assert.Equal(0, standing.RemainingMinor);
        Assert.Equal(100, standing.Percent);
    }

    [Fact]
    public void The_monthly_amount_needed_spreads_what_is_left_over_the_months_left()
    {
        // ₹1,00,000 saved with exactly 20 months to go: ₹2,00,000 / 20 = ₹10,000 a month.
        var standing = GoalMath.Stand(Ring, 1_00_000_00, new DateOnly(2027, 1, 1));

        Assert.Equal(20, GoalMath.MonthsLeft(new DateOnly(2027, 1, 1), Ring.TargetDate));
        Assert.Equal(10_000_00, standing.RequiredMonthlyMinor);
        Assert.Equal(33, standing.Percent);
    }

    [Fact]
    public void A_part_month_counts_as_a_month_and_the_last_month_is_never_zero()
    {
        Assert.Equal(1, GoalMath.MonthsLeft(new DateOnly(2028, 9, 1), new DateOnly(2028, 9, 1)));
        Assert.Equal(2, GoalMath.MonthsLeft(new DateOnly(2028, 7, 20), new DateOnly(2028, 9, 1)));
    }

    [Fact]
    public void Spending_from_a_goal_takes_it_down_and_saving_toward_it_adds()
    {
        var mine = new Party { Kind = PartyKind.Container, ContainerId = Guid.NewGuid() };
        var savings = new Party { Kind = PartyKind.Container, ContainerId = Guid.NewGuid() };
        var jeweller = new Party { Kind = PartyKind.External, Name = "Tanishq" };

        Assert.Equal(-50_000_00, GoalMath.ContributionOf(Make(mine, jeweller, 50_000_00)));
        Assert.Equal(10_000_00, GoalMath.ContributionOf(Make(mine, savings, 10_000_00)));
        Assert.Equal(5_000_00, GoalMath.ContributionOf(Make(jeweller, mine, 5_000_00)));
    }

    private static Transaction Make(Party source, Party destination, long minor) => new()
    {
        SourceParty = source,
        SourcePartyId = source.Id,
        DestinationParty = destination,
        DestinationPartyId = destination.Id,
        SourceAmountMinor = minor,
        DestinationAmountMinor = minor,
        OccurredOn = new DateOnly(2027, 1, 1),
    };

    [Fact]
    public void The_recent_pace_is_what_went_in_over_the_last_three_months_as_a_monthly_figure()
    {
        var today = new DateOnly(2026, 12, 1);
        DateOnly[] when = [today.AddDays(-10), today.AddDays(-40), today.AddDays(-70), today.AddDays(-200)];

        // 30,000 in the last 90 days is 10,000 a month; the old one is outside the window.
        var pace = GoalMath.MonthlyPace(when.Zip(new long[] { 10_000_00, 10_000_00, 10_000_00, 50_000_00 }), Ring.StartDate, today);

        Assert.Equal(10_000_00, pace);
    }

    [Fact]
    public void A_goal_started_recently_is_paced_over_the_time_since_it_started_not_ninety_days()
    {
        var today = Ring.StartDate.AddDays(30);

        Assert.Equal(6_000_00, GoalMath.MonthlyPace([(today.AddDays(-5), 6_000_00)], Ring.StartDate, today));
    }

    [Fact]
    public void At_ten_thousand_a_month_the_remaining_ninety_thousand_takes_nine_months()
    {
        var today = new DateOnly(2027, 3, 15);

        Assert.Equal(new DateOnly(2027, 12, 15), GoalMath.ProjectedFinish(2_10_000_00, 3_00_000_00, 10_000_00, today));
    }

    [Fact]
    public void Nothing_going_in_means_no_projection_and_a_reached_goal_is_done_today()
    {
        var today = new DateOnly(2027, 3, 15);

        Assert.Null(GoalMath.ProjectedFinish(1_00_000_00, 3_00_000_00, 0, today));
        Assert.Equal(today, GoalMath.ProjectedFinish(3_00_000_00, 3_00_000_00, 0, today));
    }
}
