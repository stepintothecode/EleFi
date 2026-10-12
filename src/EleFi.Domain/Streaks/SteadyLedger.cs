namespace EleFi.Domain.Streaks;

/// <summary>How a finished week went.</summary>
public enum WeekResult
{
    /// <summary>Nothing left to review and no plan from it left open.</summary>
    Clean = 0,

    /// <summary>Not clean, but a freeze kept the streak going.</summary>
    Frozen = 1,

    /// <summary>Not clean, and no freeze to spend: the streak started again.</summary>
    Missed = 2,
}

/// <summary>One finished week.</summary>
/// <param name="Monday">The week's first day.</param>
/// <param name="Result">How it went.</param>
public sealed record WeekStanding(DateOnly Monday, WeekResult Result);

/// <summary>The week in progress, and what is left to tidy in it so far.</summary>
/// <param name="Monday">Its first day.</param>
/// <param name="ToReview">Transactions from it still flagged Needs Review.</param>
/// <param name="PlansOpen">Plans due in it, up to today, still open.</param>
public sealed record ThisWeek(DateOnly Monday, int ToReview, int PlansOpen)
{
    /// <summary>True when nothing is left to tidy so far.</summary>
    public bool IsTidy => ToReview == 0 && PlansOpen == 0;
}

/// <summary>Where the Steady ledger streak stands.</summary>
/// <param name="Weeks">Clean weeks in a row, counting this one while it is tidy.</param>
/// <param name="Freezes">Freezes in hand, spent automatically on a missed week.</param>
/// <param name="ThisWeek">The week in progress.</param>
/// <param name="Recent">The last finished weeks, oldest first, at most eight.</param>
/// <param name="NextMilestone">The next milestone ahead, or null past the last.</param>
/// <param name="MilestoneReached">The milestone the streak is on right now, if it is on one.</param>
public sealed record SteadyStreak(
    int Weeks,
    int Freezes,
    ThisWeek ThisWeek,
    IReadOnlyList<WeekStanding> Recent,
    int? NextMilestone,
    int? MilestoneReached);

/// <summary>
/// The Steady ledger streak: weeks of keeping the ledger true.
/// </summary>
/// <remarks>
/// <para>
/// The unit is the week, not the day, and the habit is tidying, not recording: a week counts
/// when nothing from it still needs review and no plan due in it is left open. A week with
/// nothing in it counts too; there was nothing to tidy. That is about a minute a week when
/// alerts record most payments, which is what makes it a habit worth keeping.
/// </para>
/// <para>
/// Worked out from the ledger as it is now, so tidying an old week later mends it. The week in
/// progress adds one while it is tidy and never breaks the run before it ends. A freeze is
/// earned for every four clean weeks, at most two held, and spent on a missed week without
/// asking, so a holiday does not reset a year.
/// </para>
/// </remarks>
public static class SteadyLedger
{
    /// <summary>Clean weeks that earn one freeze.</summary>
    public const int WeeksPerFreeze = 4;

    /// <summary>The most freezes held at once.</summary>
    public const int MaximumFreezes = 2;

    /// <summary>How many finished weeks <see cref="SteadyStreak.Recent"/> shows.</summary>
    public const int RecentWeeks = 8;

    /// <summary>The streaks Ele celebrates.</summary>
    public static IReadOnlyList<int> Milestones { get; } = [4, 12, 26, 52];

    /// <summary>The Monday a day's week starts on.</summary>
    /// <param name="day">The day.</param>
    public static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    /// <summary>Works out the streak.</summary>
    /// <param name="startedOn">The day the user started keeping the ledger, or null when nothing is recorded.</param>
    /// <param name="needsReviewOn">The day of every transaction still flagged Needs Review.</param>
    /// <param name="openPlansDueOn">The due date of every plan still open.</param>
    /// <param name="today">Today.</param>
    public static SteadyStreak Compute(
        DateOnly? startedOn,
        IEnumerable<DateOnly> needsReviewOn,
        IEnumerable<DateOnly> openPlansDueOn,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(needsReviewOn);
        ArgumentNullException.ThrowIfNull(openPlansDueOn);

        var thisMonday = MondayOf(today);
        var reviews = needsReviewOn.ToList();
        var plans = openPlansDueOn.ToList();

        var current = new ThisWeek(
            thisMonday,
            reviews.Count(d => d >= thisMonday && d <= today),
            plans.Count(d => d >= thisMonday && d <= today));

        if (startedOn is not { } started || started > today)
        {
            return new SteadyStreak(0, 0, current, [], Milestones[0], null);
        }

        var dirty = reviews.Concat(plans).Select(MondayOf).ToHashSet();
        var run = 0;
        var freezes = 0;
        var sinceFreeze = 0;
        var weeks = new List<WeekStanding>();

        for (var monday = MondayOf(started); monday < thisMonday; monday = monday.AddDays(7))
        {
            WeekResult result;
            if (!dirty.Contains(monday))
            {
                run++;
                result = WeekResult.Clean;
                if (++sinceFreeze == WeeksPerFreeze)
                {
                    freezes = Math.Min(freezes + 1, MaximumFreezes);
                    sinceFreeze = 0;
                }
            }
            else if (freezes > 0)
            {
                freezes--;
                result = WeekResult.Frozen;
            }
            else
            {
                run = 0;
                sinceFreeze = 0;
                result = WeekResult.Missed;
            }

            weeks.Add(new WeekStanding(monday, result));
        }

        var total = run + (current.IsTidy ? 1 : 0);
        return new SteadyStreak(
            total,
            freezes,
            current,
            [.. weeks.Skip(Math.Max(0, weeks.Count - RecentWeeks))],
            Milestones.Cast<int?>().FirstOrDefault(m => m > total),
            Milestones.Contains(total) ? total : null);
    }
}
