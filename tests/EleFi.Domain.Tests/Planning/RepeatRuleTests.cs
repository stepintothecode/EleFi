using EleFi.Domain.Planning;

namespace EleFi.Domain.Tests.Planning;

public class RepeatRuleTests
{
    [Fact]
    public void A_one_off_plan_has_no_next_date()
    {
        Assert.Null(RepeatRule.Never.NextAfter(new DateOnly(2026, 10, 5), 5));
    }

    [Theory]
    [InlineData(RepeatFrequency.Daily, 1, 2026, 10, 6)]
    [InlineData(RepeatFrequency.Daily, 3, 2026, 10, 8)]
    [InlineData(RepeatFrequency.Weekly, 1, 2026, 10, 12)]
    [InlineData(RepeatFrequency.Weekly, 2, 2026, 10, 19)]
    [InlineData(RepeatFrequency.Monthly, 1, 2026, 11, 5)]
    [InlineData(RepeatFrequency.Monthly, 3, 2027, 1, 5)]
    [InlineData(RepeatFrequency.Yearly, 1, 2027, 10, 5)]
    public void The_next_date_follows_the_rule(RepeatFrequency frequency, int interval, int y, int m, int d)
    {
        Assert.Equal(new DateOnly(y, m, d), new RepeatRule(frequency, interval).NextAfter(new DateOnly(2026, 10, 5), 5));
    }

    [Fact]
    public void A_monthly_plan_on_the_31st_keeps_to_the_31st_after_a_short_month()
    {
        var monthly = new RepeatRule(RepeatFrequency.Monthly);

        var february = monthly.NextAfter(new DateOnly(2027, 1, 31), 31)!.Value;
        var march = monthly.NextAfter(february, 31)!.Value;

        // Not the 28th for ever after the first February.
        Assert.Equal(new DateOnly(2027, 2, 28), february);
        Assert.Equal(new DateOnly(2027, 3, 31), march);
    }

    [Theory]
    [InlineData(RepeatFrequency.None, 1, "Once")]
    [InlineData(RepeatFrequency.Monthly, 1, "Every month")]
    [InlineData(RepeatFrequency.Weekly, 2, "Every 2 weeks")]
    public void A_rule_describes_itself_in_plain_words(RepeatFrequency frequency, int interval, string words)
    {
        Assert.Equal(words, new RepeatRule(frequency, interval).Describe());
    }

    // 2026-10-05 is a Monday.
    [Theory]
    [InlineData(2026, 10, 5, 2026, 10, 8)]
    [InlineData(2026, 10, 8, 2026, 10, 12)]
    [InlineData(2026, 10, 6, 2026, 10, 8)]
    public void Weekly_on_chosen_days_goes_to_the_next_chosen_day(int y, int m, int d, int ny, int nm, int nd)
    {
        var mondaysAndThursdays = new RepeatRule(RepeatFrequency.Weekly, 1, RepeatDays.Monday | RepeatDays.Thursday);

        Assert.Equal(new DateOnly(ny, nm, nd), mondaysAndThursdays.NextAfter(new DateOnly(y, m, d), 5));
    }

    [Fact]
    public void Every_two_weeks_on_chosen_days_skips_the_week_between()
    {
        var fortnightly = new RepeatRule(RepeatFrequency.Weekly, 2, RepeatDays.Monday | RepeatDays.Thursday);

        // Thursday is the last chosen day this week; the next is Monday two weeks on.
        Assert.Equal(new DateOnly(2026, 10, 19), fortnightly.NextAfter(new DateOnly(2026, 10, 8), 5));
    }

    [Fact]
    public void Last_day_of_the_month_lands_on_each_months_last_day()
    {
        var monthEnd = new RepeatRule(RepeatFrequency.Monthly);

        var november = monthEnd.NextAfter(new DateOnly(2026, 10, 31), RepeatRule.LastDay)!.Value;
        var december = monthEnd.NextAfter(november, RepeatRule.LastDay)!.Value;
        var february = monthEnd.NextAfter(new DateOnly(2027, 1, 31), RepeatRule.LastDay)!.Value;

        Assert.Equal(new DateOnly(2026, 11, 30), november);
        Assert.Equal(new DateOnly(2026, 12, 31), december);
        Assert.Equal(new DateOnly(2027, 2, 28), february);
    }

    [Fact]
    public void Chosen_days_and_the_last_day_read_as_words()
    {
        Assert.Equal("Every week on Mon, Thu", new RepeatRule(RepeatFrequency.Weekly, 1, RepeatDays.Monday | RepeatDays.Thursday).Describe());
        Assert.Equal("Every month on the last day", new RepeatRule(RepeatFrequency.Monthly).Describe(RepeatRule.LastDay));
    }
}
