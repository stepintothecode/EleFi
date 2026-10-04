using EleFi.Domain.Alerts;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Tests.Alerts;

/// <summary>
/// Pairing the bank's SMS with the Payment App's notification for the same payment.
/// </summary>
public class AlertCorroborationTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 1, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_app_notification_for_an_sms_payment_is_its_partner()
    {
        var sms = Suggestion(AlertChannel.Sms, 45000, At);

        var partner = AlertCorroboration.FindPartner([sms], Alert(AlertChannel.PaymentApp, 45000), At.AddSeconds(40));

        Assert.Same(sms, partner);
    }

    [Fact]
    public void A_different_amount_is_a_different_payment()
    {
        var sms = Suggestion(AlertChannel.Sms, 45000, At);

        Assert.Null(AlertCorroboration.FindPartner([sms], Alert(AlertChannel.PaymentApp, 45001), At));
    }

    [Fact]
    public void A_different_direction_is_a_different_payment()
    {
        var sms = Suggestion(AlertChannel.Sms, 45000, At);

        Assert.Null(AlertCorroboration.FindPartner(
            [sms], Alert(AlertChannel.PaymentApp, 45000, TransactionKind.Credit), At));
    }

    [Fact]
    public void Two_alerts_from_the_same_channel_never_merge()
    {
        // Two ₹20 chais ten minutes apart are two payments, not one.
        var first = Suggestion(AlertChannel.PaymentApp, 2000, At);

        Assert.Null(AlertCorroboration.FindPartner([first], Alert(AlertChannel.PaymentApp, 2000), At.AddMinutes(10)));
    }

    [Fact]
    public void A_suggestion_that_already_has_both_channels_takes_no_third()
    {
        var merged = Suggestion(AlertChannel.Sms, 45000, At);
        merged.Evidence |= AlertEvidence.PaymentApp;

        Assert.Null(AlertCorroboration.FindPartner([merged], Alert(AlertChannel.PaymentApp, 45000), At));
    }

    [Fact]
    public void Alerts_outside_the_window_are_not_paired()
    {
        var sms = Suggestion(AlertChannel.Sms, 45000, At);

        Assert.Null(AlertCorroboration.FindPartner(
            [sms], Alert(AlertChannel.PaymentApp, 45000), At + AlertCorroboration.Window + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void An_sms_arriving_before_the_app_notification_still_pairs()
    {
        var app = Suggestion(AlertChannel.PaymentApp, 45000, At);

        // Operators sometimes deliver the bank's SMS before the app has posted, and the
        // window is measured both ways.
        Assert.Same(app, AlertCorroboration.FindPartner([app], Alert(AlertChannel.Sms, 45000), At.AddMinutes(-3)));
    }

    [Fact]
    public void The_nearest_in_time_wins_when_several_could_pair()
    {
        var early = Suggestion(AlertChannel.Sms, 45000, At.AddMinutes(-12));
        var close = Suggestion(AlertChannel.Sms, 45000, At.AddMinutes(-1));

        Assert.Same(close, AlertCorroboration.FindPartner([early, close], Alert(AlertChannel.PaymentApp, 45000), At));
    }

    [Fact]
    public void A_late_alert_for_a_payment_already_added_finds_it_so_it_is_not_offered_again()
    {
        var done = Suggestion(AlertChannel.PaymentApp, 45000, At);
        done.State = SuggestionState.Confirmed;

        // The user added it from the app's alert; the bank's SMS turns up a minute later.
        Assert.Same(done, AlertCorroboration.FindPartner([done], Alert(AlertChannel.Sms, 45000), At.AddMinutes(1)));
    }

    [Fact]
    public void A_card_bill_from_the_app_pairs_with_the_banks_debit()
    {
        var sms = Suggestion(AlertChannel.Sms, 500000, At);

        var partner = AlertCorroboration.FindPartner(
            [sms], Alert(AlertChannel.PaymentApp, 500000, TransactionKind.SelfTransfer), At);

        // The bank cannot tell a card bill from a shop, so its Debit is the same movement.
        Assert.Same(sms, partner);
    }

    internal static CaptureSuggestion Suggestion(AlertChannel channel, long minor, DateTimeOffset at) =>
        CaptureSuggestion.FromAlert(
            Alert(channel, minor), "fp", null, DateOnly.FromDateTime(at.UtcDateTime), at, TimeSpan.FromDays(7));

    internal static ParsedAlert Alert(
        AlertChannel channel,
        long minor,
        TransactionKind direction = TransactionKind.Debit,
        string? counterparty = null,
        string? last4 = null,
        string? note = null,
        string? app = null) =>
        new(Guid.NewGuid(), minor, "INR", direction, last4, counterparty, null, note, channel, app);
}
