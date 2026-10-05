using EleFi.Application.Planning;
using EleFi.Application.Tests.Support;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Domain.Planning;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Tests.Planning;

/// <summary>The Planner: making plans, ticking them off against the ledger, and repeats.</summary>
public class PlanServiceTests
{
    private readonly InMemoryLedger _ledger = new();
    private readonly InMemoryPlans _plans = new();
    private readonly MovableClock _clock = new();
    private readonly Container _sbi;

    public PlanServiceTests() => _sbi = _ledger.AddContainer("SBI", ContainerKind.BankAccount);

    [Fact]
    public async Task A_plan_needs_a_name()
    {
        var result = await Service().CreateAsync(new PlanDraft("  ", _clock.Today));

        Assert.False(result.Succeeded);
        Assert.Empty(_plans.Rows);
    }

    [Fact]
    public async Task A_monthly_plan_keeps_to_the_day_it_was_first_due()
    {
        var result = await Service().CreateAsync(new PlanDraft(
            "SIP", new DateOnly(2027, 1, 31), 5_000_00, Repeat: new RepeatRule(RepeatFrequency.Monthly)));

        Assert.Equal(31, result.Plan!.AnchorDay);
        Assert.Equal(result.Plan.Id, result.Plan.SeriesId);
    }

    [Fact]
    public async Task Completing_a_repeating_plan_links_it_and_puts_the_next_one_on_the_list()
    {
        var service = Service();
        var sip = (await service.CreateAsync(new PlanDraft(
            "SIP", _clock.Today, 5_000_00, ContainerId: _sbi.Id, Repeat: new RepeatRule(RepeatFrequency.Monthly)))).Plan!;
        var spent = await SpendAsync(5_000_00);

        await service.CompleteAsync(sip.Id, spent.Id);

        Assert.True(sip.IsDone);
        Assert.Equal(spent.Id, sip.TransactionId);
        var next = Assert.Single(await service.OpenAsync());
        Assert.Equal(_clock.Today.AddMonths(1), next.DueOn);
    }

    [Fact]
    public async Task Candidates_are_the_matching_transactions_not_yet_claimed()
    {
        var service = Service();
        var sip = (await service.CreateAsync(new PlanDraft("SIP", _clock.Today, 5_000_00, ContainerId: _sbi.Id))).Plan!;
        var match = await SpendAsync(5_000_00);
        await SpendAsync(4_999_00);

        var candidates = await service.CandidatesAsync(sip.Id);

        Assert.Equal([match.Id], candidates.Select(c => c.Id));
    }

    [Fact]
    public async Task A_transaction_recorded_from_an_sms_ticks_off_the_matching_plan()
    {
        var service = Service();
        var sip = (await service.CreateAsync(new PlanDraft("SIP", _clock.Today, 5_000_00, ContainerId: _sbi.Id))).Plan!;
        var spent = await SpendAsync(5_000_00);

        var ticked = await service.CompleteMatchingAsync(spent);

        Assert.Same(sip, ticked);
        Assert.Equal(spent.Id, sip.TransactionId);
    }

    [Fact]
    public async Task A_transaction_already_linked_to_one_plan_never_ticks_off_another()
    {
        var service = Service();
        var first = (await service.CreateAsync(new PlanDraft("First", _clock.Today, 100))).Plan!;
        await service.CreateAsync(new PlanDraft("Second", _clock.Today, 100));
        var spent = await SpendAsync(100);
        await service.CompleteAsync(first.Id, spent.Id);

        Assert.Null(await service.CompleteMatchingAsync(spent));
    }

    [Fact]
    public async Task Reopening_a_repeating_plan_withdraws_the_occurrence_its_completion_created()
    {
        var service = Service();
        var rent = (await service.CreateAsync(new PlanDraft(
            "Rent", _clock.Today, 100, Repeat: new RepeatRule(RepeatFrequency.Monthly)))).Plan!;
        await service.CompleteAsync(rent.Id, null);

        await service.ReopenAsync(rent.Id);

        var open = Assert.Single(await service.OpenAsync());
        Assert.Same(rent, open);
        Assert.Empty(await service.CompletedAsync());
    }

    [Fact]
    public async Task A_transfer_plan_cannot_move_money_into_the_container_it_leaves()
    {
        var result = await Service().CreateAsync(new PlanDraft(
            "Loop", _clock.Today, 100, TransactionKind.SelfTransfer, _sbi.Id, DestinationContainerId: _sbi.Id));

        Assert.False(result.Succeeded);
    }

    private PlanService Service() => new(_plans, _ledger.Transactions, _clock);

    private async Task<Transaction> SpendAsync(long minor)
    {
        var shop = await _ledger.Parties.GetOrCreateExternalAsync("Fund house");
        var result = await new CaptureService(_ledger.Transactions, _ledger.Parties, _ledger.Apps, _ledger.Labels, _clock)
            .CaptureAsync(new CaptureRequest(_ledger.PartyOf(_sbi).Id, shop.Id, minor, "INR", minor, "INR", _clock.Today));

        Assert.True(result.Succeeded, result.Error);
        return result.Transaction!;
    }
}
