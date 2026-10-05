using EleFi.Domain.Filters;

namespace EleFi.Domain.Tests.Filters;

public class DatePresetTests
{
    // A Thursday.
    private static readonly DateOnly Today = new(2026, 10, 8);

    [Fact]
    public void Today_is_just_today()
    {
        Assert.Equal((Today, Today), DatePreset.Today.Range(Today));
    }

    [Fact]
    public void This_week_starts_on_monday()
    {
        Assert.Equal((new DateOnly(2026, 10, 5), Today), DatePreset.ThisWeek.Range(Today));
    }

    [Fact]
    public void This_week_on_a_sunday_still_starts_on_the_monday_before()
    {
        var sunday = new DateOnly(2026, 10, 11);

        Assert.Equal((new DateOnly(2026, 10, 5), sunday), DatePreset.ThisWeek.Range(sunday));
    }

    [Fact]
    public void This_week_on_a_monday_is_just_that_day()
    {
        var monday = new DateOnly(2026, 10, 5);

        Assert.Equal((monday, monday), DatePreset.ThisWeek.Range(monday));
    }

    [Fact]
    public void Last_month_is_the_whole_previous_month_across_a_year_boundary()
    {
        Assert.Equal(
            (new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 31)),
            DatePreset.LastMonth.Range(new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void The_financial_year_before_april_began_the_april_before()
    {
        Assert.Equal(
            (new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31)),
            DatePreset.ThisFinancialYear.Range(new DateOnly(2026, 3, 31)));
    }

    [Theory]
    [InlineData(DatePreset.AllTime)]
    [InlineData(DatePreset.Custom)]
    public void All_time_and_custom_carry_no_dates_of_their_own(DatePreset preset)
    {
        Assert.Equal(((DateOnly?)null, (DateOnly?)null), preset.Range(Today));
    }
}
