using EleFi.Domain.Planning;

namespace EleFi.Domain.Tests.Planning;

public class PlanTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    [Theory]
    [InlineData(-1, PlanBucket.Overdue)]
    [InlineData(0, PlanBucket.Today)]
    [InlineData(1, PlanBucket.ThisWeek)]
    [InlineData(6, PlanBucket.ThisWeek)]
    [InlineData(7, PlanBucket.Later)]
    public void An_open_plan_sits_in_the_bucket_for_its_due_date(int days, PlanBucket bucket)
    {
        Assert.Equal(bucket, new Plan { DueOn = Today.AddDays(days) }.BucketOn(Today));
    }

    [Fact]
    public void Completing_a_one_off_plan_marks_it_done_and_creates_nothing()
    {
        var plan = new Plan { Title = "Pay Rahul back", DueOn = Today };
        var transaction = Guid.NewGuid();

        var next = plan.Complete(DateTimeOffset.UnixEpoch, transaction);

        Assert.True(plan.IsDone);
        Assert.Equal(transaction, plan.TransactionId);
        Assert.Null(next);
    }

    [Fact]
    public void Completing_a_monthly_plan_creates_next_months_in_the_same_series()
    {
        var series = Guid.NewGuid();
        var sip = new Plan
        {
            Title = "SIP",
            AmountMinor = 5_000_00,
            DueOn = new DateOnly(2026, 10, 5),
            AnchorDay = 5,
            SeriesId = series,
            Repeat = new RepeatRule(RepeatFrequency.Monthly),
        };

        var next = sip.Complete(DateTimeOffset.UnixEpoch, null)!;

        Assert.Equal(new DateOnly(2026, 11, 5), next.DueOn);
        Assert.Equal(series, next.SeriesId);
        Assert.Equal(5_000_00, next.AmountMinor);
        Assert.False(next.IsDone);
        Assert.NotEqual(sip.Id, next.Id);
    }

    [Fact]
    public void A_missed_month_stays_overdue_rather_than_being_replaced()
    {
        var sip = new Plan { DueOn = Today.AddMonths(-1), Repeat = new RepeatRule(RepeatFrequency.Monthly) };

        // Nothing rolls forward on its own: only completing it does.
        Assert.Equal(PlanBucket.Overdue, sip.BucketOn(Today));
    }
}
