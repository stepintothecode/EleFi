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
}
