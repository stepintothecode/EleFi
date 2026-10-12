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

    [Fact]
    public async Task Skipping_puts_this_occurrence_away_without_a_transaction_and_rolls_on()
    {
        var service = Service();
        var sip = (await service.CreateAsync(new PlanDraft("SIP", _clock.Today, 5_000_00, Repeat: new RepeatRule(RepeatFrequency.Monthly)))).Plan!;

        await service.SkipAsync(sip.Id);

        Assert.True(sip.IsSkipped);
        Assert.Null(sip.TransactionId);
        Assert.Equal(_clock.Today.AddMonths(1), Assert.Single(await service.OpenAsync()).DueOn);
    }

    [Fact]
    public async Task The_last_day_of_the_month_an_end_date_and_a_count_are_kept_from_the_form()
    {
        var plan = (await Service().CreateAsync(new PlanDraft(
            "Card bill", new DateOnly(2026, 9, 30), 100, Repeat: new RepeatRule(RepeatFrequency.Monthly),
            LastDayOfMonth: true, RepeatUntil: new DateOnly(2027, 3, 31), RepeatCount: 6))).Plan!;

        Assert.Equal(RepeatRule.LastDay, plan.AnchorDay);
        Assert.Equal(new DateOnly(2027, 3, 31), plan.RepeatUntil);
        Assert.Equal(6, plan.RepeatCount);
    }

    [Fact]
    public async Task An_end_date_before_the_first_due_date_is_refused()
    {
        var result = await Service().CreateAsync(new PlanDraft(
            "Rent", _clock.Today, 100, Repeat: new RepeatRule(RepeatFrequency.Monthly), RepeatUntil: _clock.Today.AddDays(-1)));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task A_plans_labels_are_carried_into_the_unlabelled_transaction_it_is_ticked_off_against()
    {
        var service = Service();
        var investment = Guid.NewGuid();
        var sip = (await service.CreateAsync(new PlanDraft("SIP", _clock.Today, 5_000_00, LabelIds: [investment]))).Plan!;
        var spent = await SpendAsync(5_000_00);

        await service.CompleteAsync(sip.Id, spent.Id);

        Assert.Equal([investment], spent.Labels.Select(l => l.LabelId));
    }

    [Fact]
    public async Task A_transaction_already_labelled_keeps_its_own_labels()
    {
        var service = Service();
        var sip = (await service.CreateAsync(new PlanDraft("SIP", _clock.Today, 5_000_00, LabelIds: [Guid.NewGuid()]))).Plan!;
        var spent = await SpendAsync(5_000_00);
        var mine = Guid.NewGuid();
        spent.Labels.Add(new TransactionLabel { TransactionId = spent.Id, LabelId = mine });

        await service.CompleteAsync(sip.Id, spent.Id);

        Assert.Equal([mine], spent.Labels.Select(l => l.LabelId));
    }

    [Fact]
    public async Task Every_change_to_the_plans_reschedules_their_reminders()
    {
        var service = Service();

        var rent = (await service.CreateAsync(new PlanDraft("Rent", _clock.Today.AddDays(2), 100))).Plan!;
        Assert.Equal([rent.Id], _reminders.Last!.Select(r => r.PlanId));

        await service.DeleteAsync(rent.Id);
        Assert.Empty(_reminders.Last!);
    }

    [Fact]
    public async Task Snoozing_moves_the_reminder_to_tomorrow_and_leaves_the_due_date()
    {
        var service = Service();
        var rent = (await service.CreateAsync(new PlanDraft("Rent", _clock.Today, 100))).Plan!;

        await service.SnoozeAsync(rent.Id);

        Assert.Equal(_clock.Today.AddDays(1), rent.RemindOn);
        Assert.Equal(_clock.Today, rent.DueOn);
        Assert.Single(_reminders.Last!);
    }

    [Fact]
    public async Task Still_planned_this_month_counts_open_money_due_by_the_end_of_the_month()
    {
        var service = Service();
        var monthEnd = new DateOnly(_clock.Today.Year, _clock.Today.Month, DateTime.DaysInMonth(_clock.Today.Year, _clock.Today.Month));
        await service.CreateAsync(new PlanDraft("Rent", monthEnd, 20_000_00));
        await service.CreateAsync(new PlanDraft("Salary", _clock.Today, 90_000_00, TransactionKind.Credit));
        await service.CreateAsync(new PlanDraft("Next month", monthEnd.AddDays(1), 5_000_00));
        await service.CreateAsync(new PlanDraft("Call the bank", _clock.Today));

        var planned = await service.StillPlannedAsync();

        Assert.Equal(20_000_00, planned.OutMinor);
        Assert.Equal(90_000_00, planned.InMinor);
        Assert.Equal(3, planned.Count);
    }

    [Fact]
    public async Task A_draft_from_a_plan_puts_an_edit_back_and_a_deleted_plan_can_be_restored()
    {
        var service = Service();
        var plan = (await service.CreateAsync(new PlanDraft("SIP", _clock.Today, 5_000_00, Repeat: new RepeatRule(RepeatFrequency.Weekly, 1, RepeatDays.Monday)))).Plan!;
        var before = PlanDraft.From(plan);

        await service.UpdateAsync(plan.Id, before with { Title = "Changed", AmountMinor = 1 });
        await service.UpdateAsync(plan.Id, before);
        await service.DeleteAsync(plan.Id);
        await service.RestoreAsync(plan.Id);

        Assert.Equal("SIP", plan.Title);
        Assert.Equal(5_000_00, plan.AmountMinor);
        Assert.Equal(RepeatDays.Monday, plan.RepeatDays);
        Assert.Single(await service.OpenAsync());
    }

    private readonly RecordingReminders _reminders = new();

    private PlanService Service() => new(_plans, _ledger.Transactions, _clock, NSubstitute.Substitute.For<EleFi.Application.Abstractions.IAuditRepository>(), _reminders);

    private sealed class RecordingReminders : EleFi.Application.Abstractions.IReminderSchedule
    {
        public IReadOnlyList<PlanReminder>? Last { get; private set; }

        public void Replace(IReadOnlyList<PlanReminder> reminders) => Last = reminders;
    }

    private async Task<Transaction> SpendAsync(long minor)
    {
        var shop = await _ledger.Parties.GetOrCreateExternalAsync("Fund house");
        var result = await new CaptureService(_ledger.Transactions, _ledger.Parties, _ledger.Apps, _ledger.Labels, _clock)
            .CaptureAsync(new CaptureRequest(_ledger.PartyOf(_sbi).Id, shop.Id, minor, "INR", minor, "INR", _clock.Today));

        Assert.True(result.Succeeded, result.Error);
        return result.Transaction!;
    }
}
