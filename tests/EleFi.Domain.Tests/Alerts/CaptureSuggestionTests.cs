using EleFi.Domain.Alerts;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Tests.Alerts;

/// <summary>
/// What a suggestion holds once both the bank and the Payment App have described a payment.
/// </summary>
public class CaptureSuggestionTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 1, 13, 0, 0, TimeSpan.Zero);
    private static readonly Guid Card = Guid.NewGuid();

    [Fact]
    public void A_new_suggestion_records_which_channel_described_it()
    {
        var suggestion = CaptureSuggestion.FromAlert(
            AlertCorroborationTests.Alert(AlertChannel.PaymentApp, 45000, counterparty: "Zomato", note: "Lunch", app: "GPay"),
            "fp", null, new DateOnly(2026, 9, 1), At, TimeSpan.FromDays(7));

        Assert.Equal(AlertEvidence.PaymentApp, suggestion.Evidence);
        Assert.Equal("GPay", suggestion.PaymentAppName);
        Assert.Equal("Lunch", suggestion.Note);
        Assert.Equal(At.AddDays(7), suggestion.ExpiresAt);
        Assert.True(suggestion.IsPending);
    }

    [Fact]
    public void The_app_completes_an_sms_suggestion_with_the_real_name_and_the_note()
    {
        var fromSms = CaptureSuggestion.FromAlert(
            AlertCorroborationTests.Alert(AlertChannel.Sms, 45000, counterparty: "VPA zomato@hdfcbank", last4: "4417"),
            "sms-fp", Card, new DateOnly(2026, 9, 1), At, TimeSpan.FromDays(7));

        fromSms.Corroborate(
            AlertCorroborationTests.Alert(AlertChannel.PaymentApp, 45000, counterparty: "Zomato", note: "Lunch", app: "GPay"),
            "app-fp",
            containerId: null);

        // The bank's card, the app's name for the payee, and the app's note.
        Assert.Equal(Card, fromSms.ContainerId);
        Assert.Equal("Zomato", fromSms.CounterpartyText);
        Assert.Equal("Lunch", fromSms.Note);
        Assert.Equal("GPay", fromSms.PaymentAppName);
        Assert.Equal(AlertEvidence.Sms | AlertEvidence.PaymentApp, fromSms.Evidence);
        Assert.Equal("app-fp", fromSms.CorroboratingFingerprint);
    }

    [Fact]
    public void The_sms_completes_an_app_suggestion_with_the_card_but_keeps_the_apps_name()
    {
        var fromApp = CaptureSuggestion.FromAlert(
            AlertCorroborationTests.Alert(AlertChannel.PaymentApp, 45000, counterparty: "Zomato", app: "GPay"),
            "app-fp", null, new DateOnly(2026, 9, 1), At, TimeSpan.FromDays(7));

        fromApp.Corroborate(
            AlertCorroborationTests.Alert(AlertChannel.Sms, 45000, counterparty: "VPA zomato@hdfcbank", last4: "4417"),
            "sms-fp",
            Card);

        // Arrival order does not matter: the result is the same as the other way round.
        Assert.Equal(Card, fromApp.ContainerId);
        Assert.Equal("Zomato", fromApp.CounterpartyText);
        Assert.Equal(AlertEvidence.Sms | AlertEvidence.PaymentApp, fromApp.Evidence);
    }

    [Fact]
    public void A_matched_container_is_never_overwritten_by_a_later_alert()
    {
        var other = Guid.NewGuid();
        var suggestion = CaptureSuggestion.FromAlert(
            AlertCorroborationTests.Alert(AlertChannel.Sms, 45000), "fp", Card, new DateOnly(2026, 9, 1), At, TimeSpan.FromDays(7));

        suggestion.Corroborate(AlertCorroborationTests.Alert(AlertChannel.PaymentApp, 45000), "fp2", other);

        Assert.Equal(Card, suggestion.ContainerId);
    }

    [Fact]
    public void An_app_alert_with_no_name_leaves_the_sms_name_alone()
    {
        var suggestion = CaptureSuggestion.FromAlert(
            AlertCorroborationTests.Alert(AlertChannel.Sms, 45000, counterparty: "ZOMATO"),
            "fp", null, new DateOnly(2026, 9, 1), At, TimeSpan.FromDays(7));

        suggestion.Corroborate(AlertCorroborationTests.Alert(AlertChannel.PaymentApp, 45000, app: "GPay"), "fp2", null);

        Assert.Equal("ZOMATO", suggestion.CounterpartyText);
    }

    [Fact]
    public void Amount_and_direction_never_change_on_corroboration()
    {
        var suggestion = CaptureSuggestion.FromAlert(
            AlertCorroborationTests.Alert(AlertChannel.Sms, 45000), "fp", null, new DateOnly(2026, 9, 1), At, TimeSpan.FromDays(7));

        suggestion.Corroborate(AlertCorroborationTests.Alert(AlertChannel.PaymentApp, 45000), "fp2", null);

        Assert.Equal(45000, suggestion.AmountMinor);
        Assert.Equal(TransactionKind.Debit, suggestion.Direction);
    }

    [Fact]
    public void The_apps_card_bill_turns_the_banks_debit_into_a_self_transfer()
    {
        var suggestion = CaptureSuggestion.FromAlert(
            AlertCorroborationTests.Alert(AlertChannel.Sms, 500000, last4: "4417"), "fp", Card, new DateOnly(2026, 9, 1), At, TimeSpan.FromDays(7));

        suggestion.Corroborate(
            AlertCorroborationTests.Alert(AlertChannel.PaymentApp, 500000, TransactionKind.SelfTransfer, counterparty: "HDFC Credit Card", app: "CRED"),
            "fp2",
            null);

        Assert.Equal(TransactionKind.SelfTransfer, suggestion.Direction);
        Assert.Equal(Card, suggestion.ContainerId);
    }

    [Fact]
    public void The_banks_date_wins_even_when_the_app_alert_arrived_first()
    {
        var fromApp = CaptureSuggestion.FromAlert(
            AlertCorroborationTests.Alert(AlertChannel.PaymentApp, 45000), "fp", null, new DateOnly(2026, 9, 1), At, TimeSpan.FromDays(7));

        var bankSays = AlertCorroborationTests.Alert(AlertChannel.Sms, 45000) with { OccurredOn = new DateOnly(2026, 8, 31) };
        fromApp.Corroborate(bankSays, "fp2", Card);

        // The app's date was only "today" by default; the bank's is its own record.
        Assert.Equal(new DateOnly(2026, 8, 31), fromApp.OccurredOn);
    }

    [Fact]
    public void The_apps_time_of_day_is_kept_through_a_merge()
    {
        var fromSms = CaptureSuggestion.FromAlert(
            AlertCorroborationTests.Alert(AlertChannel.Sms, 45000), "fp", Card, new DateOnly(2026, 9, 1), At, TimeSpan.FromDays(7));

        fromSms.Corroborate(
            AlertCorroborationTests.Alert(AlertChannel.PaymentApp, 45000) with { OccurredAtTime = new TimeOnly(13, 4) },
            "fp2",
            null);

        Assert.Equal(new TimeOnly(13, 4), fromSms.OccurredAtTime);
    }
}
