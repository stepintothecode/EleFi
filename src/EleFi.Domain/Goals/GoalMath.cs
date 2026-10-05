using EleFi.Domain.Transactions;

namespace EleFi.Domain.Goals;

/// <summary>Where a goal stands: progress, what it should be by now, and what it still needs.</summary>
/// <param name="ProgressMinor">Progress so far, in minor units. May be negative or above the target.</param>
/// <param name="TargetMinor">The target.</param>
/// <param name="ExpectedMinor">What progress should be by today, on a straight line from start to target date.</param>
/// <param name="Status">On track, behind, or achieved.</param>
/// <param name="RequiredMonthlyMinor">What it needs each month from now to hit the target on time. Zero once achieved.</param>
public readonly record struct GoalStanding(
    long ProgressMinor,
    long TargetMinor,
    long ExpectedMinor,
    GoalStatus Status,
    long RequiredMonthlyMinor)
{
    /// <summary>What is left to save. Never negative.</summary>
    public long RemainingMinor => Math.Max(0, TargetMinor - ProgressMinor);

    /// <summary>Progress as a whole percentage, clamped to 0 to 100 for drawing a bar.</summary>
    public int Percent => TargetMinor <= 0
        ? 0
        : (int)Int128.Clamp((Int128)Math.Max(0, ProgressMinor) * 100 / TargetMinor, 0, 100);
}

/// <summary>
/// The arithmetic of goals, in integer minor units. Pure, so every rule is testable alone.
/// </summary>
public static class GoalMath
{
    /// <summary>
    /// What a transaction attributed to a goal does to its progress.
    /// </summary>
    /// <remarks>
    /// Money spent from a goal (a Debit, such as buying the ring) takes it down. Money moved
    /// toward it or received for it (a Self Transfer into savings, a Credit) adds to it.
    /// </remarks>
    /// <param name="transaction">The transaction, with its parties loaded.</param>
    public static long ContributionOf(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return transaction.Kind == TransactionKind.Debit
            ? -transaction.SourceAmountMinor
            : transaction.DestinationAmountMinor;
    }

    /// <summary>Works out where a goal stands today.</summary>
    /// <param name="goal">The goal.</param>
    /// <param name="progressMinor">Its progress, already derived.</param>
    /// <param name="today">Today.</param>
    public static GoalStanding Stand(Goal goal, long progressMinor, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(goal);

        var target = goal.TargetAmountMinor;
        var expected = ExpectedBy(goal, today);

        var status = progressMinor >= target
            ? GoalStatus.Achieved
            : (Int128)progressMinor * 100 >= (Int128)expected * 95 ? GoalStatus.OnTrack : GoalStatus.Behind;

        var required = status == GoalStatus.Achieved
            ? 0
            : CeilDivide(target - progressMinor, MonthsLeft(today, goal.TargetDate));

        return new GoalStanding(progressMinor, target, expected, status, required);
    }

    /// <summary>What progress should be by a day, on a straight line from start to target.</summary>
    /// <param name="goal">The goal.</param>
    /// <param name="day">The day.</param>
    public static long ExpectedBy(Goal goal, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(goal);

        var total = goal.TargetDate.DayNumber - goal.StartDate.DayNumber;
        if (total <= 0)
        {
            return goal.TargetAmountMinor;
        }

        var elapsed = Math.Clamp(day.DayNumber - goal.StartDate.DayNumber, 0, total);
        return (long)((Int128)goal.TargetAmountMinor * elapsed / total);
    }

    /// <summary>Whole months left until a date, counting a part month as one. At least one.</summary>
    /// <param name="today">Today.</param>
    /// <param name="targetDate">The target date.</param>
    public static int MonthsLeft(DateOnly today, DateOnly targetDate)
    {
        var months = ((targetDate.Year - today.Year) * 12) + targetDate.Month - today.Month;
        if (targetDate.Day > today.Day)
        {
            months++;
        }

        return Math.Max(1, months);
    }

    private static long CeilDivide(long amount, int parts) =>
        amount <= 0 ? 0 : (amount + parts - 1) / parts;
}
