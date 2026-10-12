using EleFi.Application.Streaks;
using EleFi.Application.Tests.Support;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Domain.Planning;

namespace EleFi.Application.Tests.Streaks;

/// <summary>The streak, worked out from the ledger and the plans as they are now.</summary>
public class SteadyLedgerServiceTests
{
    private readonly InMemoryLedger _ledger = new();
    private readonly InMemoryPlans _plans = new();
    private readonly MovableClock _clock = new();

    [Fact]
    public async Task Nothing_recorded_means_no_streak()
    {
        var streak = await Service().CurrentAsync();

        Assert.Equal(0, streak.Weeks);
    }

    [Fact]
    public async Task A_transaction_left_to_review_this_week_shows_as_something_to_tidy()
    {
        await SpendAsync(needsReview: true);

        var streak = await Service().CurrentAsync();

        Assert.Equal(1, streak.ThisWeek.ToReview);
        Assert.False(streak.ThisWeek.IsTidy);
    }

    [Fact]
    public async Task An_overdue_plan_from_this_week_shows_too_and_a_done_one_does_not()
    {
        await SpendAsync(needsReview: false);
        _plans.Rows.Add(new Plan { Title = "Rent", DueOn = _clock.Today });
        var done = new Plan { Title = "SIP", DueOn = _clock.Today };
        done.Complete(_clock.UtcNow, null);
        _plans.Rows.Add(done);

        var streak = await Service().CurrentAsync();

        Assert.Equal(1, streak.ThisWeek.PlansOpen);
    }

    [Fact]
    public async Task A_tidy_ledger_counts_this_week()
    {
        await SpendAsync(needsReview: false);

        Assert.Equal(1, (await Service().CurrentAsync()).Weeks);
    }

    private SteadyLedgerService Service() => new(_ledger.Transactions, _plans, _clock);

    private async Task SpendAsync(bool needsReview)
    {
        var bank = _ledger.ContainerRows.FirstOrDefault() ?? _ledger.AddContainer("SBI", ContainerKind.BankAccount);
        var shop = await _ledger.Parties.GetOrCreateExternalAsync("Shop");
        var result = await new CaptureService(_ledger.Transactions, _ledger.Parties, _ledger.Apps, _ledger.Labels, _clock)
            .CaptureAsync(new CaptureRequest(_ledger.PartyOf(bank).Id, shop.Id, 100, "INR", 100, "INR", _clock.Today, NeedsReview: needsReview));

        Assert.True(result.Succeeded, result.Error);
        result.Transaction!.CreatedAt = _clock.UtcNow;
    }
}
