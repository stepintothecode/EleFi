using EleFi.Application.Abstractions;
using EleFi.Application.Suggestions;
using EleFi.Application.Tests.Support;
using EleFi.Application.Transactions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Containers;
using NSubstitute;

namespace EleFi.Application.Tests.Suggestions;

/// <summary>
/// The front door both receivers share: the switches, the allow-list, then the prompt.
/// </summary>
public class IncomingAlertHandlerTests
{
    private const string HdfcSms = "Rs.450.00 debited from a/c XX4417 on 01-09-26 to ZOMATO";

    private readonly InMemoryLedger _ledger = new();
    private readonly InMemorySuggestions _links = new();
    private readonly MemorySettings _store = new();
    private readonly RecordingPrompts _prompts = new();
    private readonly MovableClock _clock = new();
    private readonly InMemoryPlans _plans = new();
    private readonly InMemoryNotices _notices = new();

    public IncomingAlertHandlerTests() =>
        _ledger.AddContainer("HDFC Card", ContainerKind.CreditCard, "4417");

    [Fact]
    public async Task FR_11_24_with_the_switch_off_nothing_is_read()
    {
        var result = await Handler().HandleAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow);

        Assert.False(result.Recorded);
        Assert.Empty(_ledger.TransactionRows);
        Assert.Empty(_prompts.Shown);
    }

    [Fact]
    public async Task Each_channel_has_its_own_switch()
    {
        _store.Write(AlertCaptureSettings.PaymentAppsKey, "true");
        var handler = Handler();

        var sms = await handler.HandleAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow);
        var app = await handler.HandleAsync(
            AlertChannel.PaymentApp, PaymentApps.PhonePe.Package, "Paid ₹99 to Blinkit", _clock.UtcNow);

        Assert.False(sms.Recorded);
        Assert.True(app.Recorded);
    }

    [Fact]
    public async Task A_recorded_payment_raises_a_prompt_that_opens_its_transaction()
    {
        _store.Write(AlertCaptureSettings.SmsKey, "true");

        await Handler().HandleAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow);

        var prompt = Assert.Single(_prompts.Shown);
        Assert.Equal(_ledger.TransactionRows[0].Id, prompt.TransactionId);
        Assert.Contains("HDFC Card", prompt.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_merged_alert_updates_the_same_prompt_rather_than_raising_another()
    {
        _store.Write(AlertCaptureSettings.SmsKey, "true");
        _store.Write(AlertCaptureSettings.PaymentAppsKey, "true");
        var handler = Handler();

        await handler.HandleAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow);
        await handler.HandleAsync(AlertChannel.PaymentApp, PaymentApps.GPay.Package, "Paid ₹450 to Zomato", _clock.UtcNow);

        Assert.Equal(2, _prompts.Shown.Count);
        Assert.Equal(_prompts.Shown[0].TransactionId, _prompts.Shown[1].TransactionId);
        Assert.Contains("via GPay", _prompts.Shown[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_app_that_is_not_allow_listed_is_refused_even_with_the_switch_on()
    {
        _store.Write(AlertCaptureSettings.PaymentAppsKey, "true");

        var result = await Handler().HandleAsync(AlertChannel.PaymentApp, "com.whatsapp", "Paid ₹450 to Zomato", _clock.UtcNow);

        Assert.False(result.Recorded);
        Assert.Empty(_prompts.Shown);
    }

    [Fact]
    public async Task A_recorded_payment_is_kept_in_the_notification_list_once_even_when_completed_by_a_second_alert()
    {
        _store.Write(AlertCaptureSettings.SmsKey, "true");
        _store.Write(AlertCaptureSettings.PaymentAppsKey, "true");
        var handler = Handler();

        await handler.HandleAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow);
        await handler.HandleAsync(AlertChannel.PaymentApp, PaymentApps.GPay.Package, "Paid ₹450 to Zomato", _clock.UtcNow);

        var notice = Assert.Single(_notices.Rows);
        Assert.Contains("via GPay", notice.Body, StringComparison.Ordinal);
        Assert.Equal($"transactions/{_ledger.TransactionRows[0].Id}", notice.Route);
        Assert.False(notice.IsRead);
    }

    [Fact]
    public async Task A_recorded_payment_ticks_off_the_plan_it_fulfils_and_says_so()
    {
        _store.Write(AlertCaptureSettings.SmsKey, "true");
        _plans.Rows.Add(new EleFi.Domain.Planning.Plan { Title = "Zomato order", AmountMinor = 45000, DueOn = _clock.Today });

        await Handler().HandleAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow);

        Assert.True(_plans.Rows[0].IsDone);
        Assert.Equal(_ledger.TransactionRows[0].Id, _plans.Rows[0].TransactionId);
        Assert.Contains("Plan \"Zomato order\" done", _prompts.Shown[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_from_the_prompt_soft_deletes_the_transaction_and_withdraws_the_prompt()
    {
        _store.Write(AlertCaptureSettings.SmsKey, "true");
        var handler = Handler();
        await handler.HandleAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow);
        var id = _ledger.TransactionRows[0].Id;

        await handler.DeleteFromPromptAsync(id);

        // Soft: still there, restorable from Settings.
        Assert.NotNull(_ledger.TransactionRows[0].DeletedAt);
        Assert.Equal([id], _prompts.Withdrawn);
    }

    private IncomingAlertHandler Handler()
    {
        var capture = new CaptureService(_ledger.Transactions, _ledger.Parties, _ledger.Apps, _ledger.Labels, _clock);
        var editing = new EditTransactionService(
            _ledger.Transactions, _ledger.Parties, _ledger.Labels, Substitute.For<IAuditRepository>(), _clock);
        var resolver = new AlertPartyResolver(_ledger.Containers, _ledger.Parties, _ledger.Apps, _ledger.Transactions);
        var service = new AlertCaptureService(_links, resolver, _ledger.Apps, capture, editing, _clock);

        var planner = new EleFi.Application.Planning.PlanService(_plans, _ledger.Transactions, _clock);
        return new IncomingAlertHandler(service, new AlertCaptureSettings(_store), editing, planner, _notices, _prompts, _clock);
    }

    private sealed class MemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = [];

        public string Read(string key, string fallback) => _values.GetValueOrDefault(key, fallback);

        public void Write(string key, string value) => _values[key] = value;
    }

    private sealed class RecordingPrompts : IAlertPromptSurface
    {
        public List<AlertPrompt> Shown { get; } = [];

        public List<Guid> Withdrawn { get; } = [];

        public void Show(AlertPrompt prompt) => Shown.Add(prompt);

        public void Withdraw(Guid transactionId) => Withdrawn.Add(transactionId);
    }
}
