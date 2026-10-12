using EleFi.Domain.Planning;

namespace EleFi.Domain.Tests.Planning;

/// <summary>When a plan's reminder fires: on its due day, at its time or nine in the morning.</summary>
public class PlanRemindersTests
{
    private static readonly TimeSpan Ist = TimeSpan.FromHours(5.5);
    private static readonly DateOnly Today = new(2026, 10, 11);

    // 08:00 in India on the 11th.
    private static readonly DateTimeOffset Morning = new(2026, 10, 11, 8, 0, 0, Ist);

    [Fact]
    public void A_plan_due_today_reminds_at_its_time()
    {
        var reminder = Assert.Single(Schedule(Plan(Today, new TimeOnly(18, 30))));

        Assert.Equal(new DateTimeOffset(2026, 10, 11, 18, 30, 0, Ist), reminder.FireAt);
    }

    [Fact]
    public void A_plan_with_no_time_reminds_at_nine_in_the_morning()
    {
        var reminder = Assert.Single(Schedule(Plan(Today.AddDays(3))));

        Assert.Equal(new DateTimeOffset(2026, 10, 14, 9, 0, 0, Ist), reminder.FireAt);
    }

    [Fact]
    public void A_snoozed_plan_reminds_on_the_day_it_was_snoozed_to()
    {
        var plan = Plan(Today);
        plan.RemindOn = Today.AddDays(1);

        Assert.Equal(new DateOnly(2026, 10, 12), DateOnly.FromDateTime(Assert.Single(Schedule(plan)).FireAt.DateTime));
    }

    [Fact]
    public void No_reminder_for_an_overdue_plan_a_time_already_gone_or_a_plan_done()
    {
        var done = Plan(Today.AddDays(1));
        done.Complete(Morning, null);

        Assert.Empty(Schedule(Plan(Today.AddDays(-2)), Plan(Today, new TimeOnly(7, 0)), done));
    }

    [Fact]
    public void The_reminder_says_what_and_how_much()
    {
        var sip = Plan(Today);
        sip.Title = "SIP";
        sip.AmountMinor = 5_000_00;

        var reminder = Assert.Single(Schedule(sip));

        Assert.Equal("SIP", reminder.Title);
        Assert.Contains("₹5,000.00", reminder.Body, StringComparison.Ordinal);
        Assert.Contains("due today", reminder.Body, StringComparison.Ordinal);
    }

    private static IReadOnlyList<PlanReminder> Schedule(params Plan[] plans) => PlanReminders.Schedule(plans, Morning, Ist);

    private static Plan Plan(DateOnly due, TimeOnly? time = null) => new() { Title = "Rent", DueOn = due, DueTime = time };
}
