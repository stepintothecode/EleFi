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
}
