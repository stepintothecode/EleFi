using EleFi.Domain.Filters;

namespace EleFi.Domain.Tests.Filters;

/// <summary>The date and time range rule, the one the list, the export and the dashboard share.</summary>
public class TransactionFilterTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);
    private static readonly DateOnly Friday = new(2026, 10, 9);

    private static readonly TransactionFilter MondayEveningToFridayMorning = new()
    {
        From = Monday,
        FromTime = new TimeOnly(18, 0),
        To = Friday,
        ToTime = new TimeOnly(9, 30),
    };

    [Theory]
    [InlineData(2026, 10, 5, 18, 0, true)]   // the first included minute
    [InlineData(2026, 10, 5, 17, 59, false)] // a minute before
    [InlineData(2026, 10, 7, 3, 0, true)]    // a day inside is whole
    [InlineData(2026, 10, 9, 9, 30, true)]   // the last included minute
    [InlineData(2026, 10, 9, 9, 31, false)]  // a minute after
    [InlineData(2026, 10, 4, 23, 0, false)]  // the day before
    [InlineData(2026, 10, 10, 1, 0, false)]  // the day after
    public void Times_cut_only_the_boundary_days(int y, int m, int d, int h, int min, bool covered)
    {
        Assert.Equal(covered, MondayEveningToFridayMorning.CoversMoment(new DateOnly(y, m, d), new TimeOnly(h, min)));
    }

    [Fact]
    public void A_transaction_with_no_time_on_a_boundary_day_is_kept()
    {
        // Hiding it for a time it does not have would make it vanish from its own day.
        Assert.True(MondayEveningToFridayMorning.CoversMoment(Monday, null));
        Assert.True(MondayEveningToFridayMorning.CoversMoment(Friday, null));
    }

    [Fact]
    public void A_time_without_a_date_restricts_nothing()
    {
        var filter = new TransactionFilter { FromTime = new TimeOnly(18, 0) };

        Assert.True(filter.CoversMoment(Monday, new TimeOnly(6, 0)));
    }

    [Fact]
    public void A_preset_replaces_the_dates_and_clears_any_times()
    {
        var filter = MondayEveningToFridayMorning.WithPreset(DatePreset.Today, Friday);

        Assert.Equal(Friday, filter.From);
        Assert.Equal(Friday, filter.To);
        Assert.Null(filter.FromTime);
        Assert.Null(filter.ToTime);
    }

    [Fact]
    public void Times_count_as_a_restriction()
    {
        Assert.False(new TransactionFilter { ToTime = new TimeOnly(9, 0) }.IsEmpty);
    }
}
