using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Planning;

/// <summary>One reminder to raise: which plan, what it says, and when.</summary>
/// <param name="PlanId">The plan.</param>
/// <param name="Title">The headline: the plan's title.</param>
/// <param name="Body">The detail: "₹5,000.00 to pay, due today".</param>
/// <param name="FireAt">When to raise it.</param>
public sealed record PlanReminder(Guid PlanId, string Title, string Body, DateTimeOffset FireAt);

/// <summary>
/// When open plans' reminders fire: on the due day (or the day snoozed to), at the plan's
/// time, or nine in the morning when it has none.
/// </summary>
/// <remarks>
/// Only reminders still to come are scheduled. An overdue plan sits at the top of the Plans
/// tab already; reminding about it again every time the schedule is rebuilt would be nagging.
/// Pure: plans and the time in, reminders out.
/// </remarks>
public static class PlanReminders
{
    /// <summary>The time a reminder fires for a plan with no time of its own.</summary>
    public static TimeOnly DefaultTime { get; } = new(9, 0);

    /// <summary>The reminders to schedule, soonest first.</summary>
    /// <param name="plans">The open plans.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="localOffset">The device's offset from UTC, for turning a day and time into an instant.</param>
    public static IReadOnlyList<PlanReminder> Schedule(IEnumerable<Plan> plans, DateTimeOffset now, TimeSpan localOffset)
    {
        ArgumentNullException.ThrowIfNull(plans);

        var today = DateOnly.FromDateTime(now.ToOffset(localOffset).DateTime);

        return [.. plans
            .Where(p => !p.IsDone && p.ReminderDay >= today)
            .Select(p => new PlanReminder(
                p.Id,
                p.Title,
                Body(p),
                new DateTimeOffset(p.ReminderDay.ToDateTime(p.DueTime ?? DefaultTime), localOffset)))
            .Where(r => r.FireAt > now)
            .OrderBy(r => r.FireAt)];
    }

    // Read on the day it fires: due that day, or, when snoozed, due on an earlier one.
    private static string Body(Plan plan)
    {
        var when = plan.DueOn >= plan.ReminderDay
            ? "due today"
            : $"was due {plan.DueOn.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture)}";

        if (plan.AmountMinor is not { } minor)
        {
            return char.ToUpperInvariant(when[0]) + when[1..];
        }

        var verb = plan.Direction switch
        {
            TransactionKind.Credit => "to receive",
            TransactionKind.SelfTransfer => "to move",
            _ => "to pay",
        };

        return $"{MoneyText.ToDisplayString(Money.Money.FromMinor(minor, Currency.Of(plan.CurrencyCode)))} {verb}, {when}";
    }
}
