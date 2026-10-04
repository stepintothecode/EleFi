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

    private readonly InMemorySuggestions _suggestions = new();
    private readonly MemorySettings _store = new();
    private readonly RecordingPrompts _prompts = new();
    private readonly MovableClock _clock = new();
    private readonly Container _card = new() { Name = "HDFC Card", Kind = ContainerKind.CreditCard, AccountNumberLast4 = "4417" };

    [Fact]
    public async Task FR_11_24_with_the_switch_off_nothing_is_read()
    {
        var handler = Handler();

        var result = await handler.HandleAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow);

        Assert.False(result.Created);
        Assert.Empty(_suggestions.Rows);
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

        Assert.False(sms.Created);
        Assert.True(app.Created);
    }

    [Fact]
    public async Task A_matched_alert_raises_a_prompt_naming_the_container()
    {
        _store.Write(AlertCaptureSettings.SmsKey, "true");
        var handler = Handler();

        await handler.HandleAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow);

        var prompt = Assert.Single(_prompts.Shown);
        Assert.Equal(_suggestions.Rows[0].Id, prompt.SuggestionId);
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
        Assert.Equal(_prompts.Shown[0].SuggestionId, _prompts.Shown[1].SuggestionId);
        Assert.Contains("via GPay", _prompts.Shown[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_app_that_is_not_allow_listed_is_refused_even_with_the_switch_on()
    {
        _store.Write(AlertCaptureSettings.PaymentAppsKey, "true");
        var handler = Handler();

        var result = await handler.HandleAsync(AlertChannel.PaymentApp, "com.whatsapp", "Paid ₹450 to Zomato", _clock.UtcNow);

        Assert.False(result.Created);
        Assert.Empty(_prompts.Shown);
    }

    [Fact]
    public async Task Dismissing_from_the_prompt_destroys_the_suggestion_and_withdraws_the_prompt()
    {
        _store.Write(AlertCaptureSettings.SmsKey, "true");
        var handler = Handler();
        await handler.HandleAsync(AlertChannel.Sms, "VM-HDFCBK", HdfcSms, _clock.UtcNow);
        var id = _suggestions.Rows[0].Id;

        await handler.DismissFromPromptAsync(id);

        Assert.Empty(_suggestions.Rows);
        Assert.Equal([id], _prompts.Withdrawn);
    }

    private IncomingAlertHandler Handler()
    {
        var containers = Substitute.For<IContainerRepository>();
        containers.FindByLast4Async("4417", Arg.Any<CancellationToken>()).Returns([_card]);
        containers.FindAsync(_card.Id, Arg.Any<CancellationToken>()).Returns(_card);

        var parties = Substitute.For<IPartyRepository>();
        var apps = Substitute.For<IAppRepository>();
        var capture = new CaptureService(
            Substitute.For<ITransactionRepository>(), parties, apps, Substitute.For<ILabelRepository>(), _clock);

        var service = new SuggestionService(_suggestions, containers, parties, apps, capture, _clock);
        return new IncomingAlertHandler(service, new AlertCaptureSettings(_store), containers, _prompts);
    }

    private sealed class MemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = [];

        public string Read(string key, string fallback) => _values.GetValueOrDefault(key, fallback);

        public void Write(string key, string value) => _values[key] = value;
    }

    private sealed class RecordingPrompts : ISuggestionPromptSurface
    {
        public List<SuggestionPrompt> Shown { get; } = [];

        public List<Guid> Withdrawn { get; } = [];

        public void Show(SuggestionPrompt prompt) => Shown.Add(prompt);

        public void Withdraw(Guid suggestionId) => Withdrawn.Add(suggestionId);
    }
}
