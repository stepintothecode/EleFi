using EleFi.Domain.Alerts;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Tests.Alerts;

/// <summary>
/// The corpus, run against exactly the rules the app ships.
/// </summary>
/// <remarks>
/// Payment App wording is not published anywhere, so these samples are the phrasings the
/// rules are written to accept. A new phrasing seen in the wild is a new line here first.
/// </remarks>
public class BuiltInParseRulesTests
{
    private static readonly string GPay = PaymentApps.GPay.Package;
    private static readonly string PhonePe = PaymentApps.PhonePe.Package;
    private static readonly string Paytm = PaymentApps.Paytm.Package;
    private static readonly string Amazon = PaymentApps.AmazonPay.Package;

    [Fact]
    public void Every_shipped_rule_compiles()
    {
        foreach (var rule in BuiltInParseRules.Create(DateTimeOffset.UnixEpoch))
        {
            Assert.True(rule.TryCompile(out _, out var error), $"{rule.Name}: {error}");
        }
    }

    [Fact]
    public void Shipped_rule_names_are_unique_because_seeding_matches_on_them()
    {
        var names = BuiltInParseRules.All.Select(r => r.Name).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_payment_app_has_a_paid_and_a_received_rule()
    {
        foreach (var app in PaymentApps.All)
        {
            var rules = BuiltInParseRules.All.Where(r => r.AppName == app.Name).ToList();

            Assert.Contains(rules, r => r.Direction == TransactionKind.Debit);
            Assert.Contains(rules, r => r.Direction == TransactionKind.Credit);
            Assert.All(rules, r => Assert.Equal(AlertChannel.PaymentApp, r.Channel));
        }
    }

    [Theory]
    [MemberData(nameof(AlertParserTests.OtpMessages), MemberType = typeof(AlertParserTests))]
    public void SM4_no_shipped_rule_on_either_channel_parses_a_one_time_passcode(string message)
    {
        var senders = new[] { "VM-HDFCBK", "AD-ICICIB", "JD-SBIBNK", "VK-AXISBK", "AX-KOTAKB" };

        foreach (var sender in senders)
        {
            Assert.Null(Parse(AlertChannel.Sms, sender, message));
        }

        foreach (var app in PaymentApps.All)
        {
            Assert.Null(Parse(AlertChannel.PaymentApp, app.Package, message));
        }
    }

    [Theory]
    [InlineData("Payment successful\nYou paid ₹450.00 to Zomato", 45000, "Zomato")]
    [InlineData("₹450 paid to Zomato", 45000, "Zomato")]
    [InlineData("Paid ₹1,250.50 to Rahul Sharma", 125050, "Rahul Sharma")]
    [InlineData("Sent ₹200 to Chai Point via UPI", 20000, "Chai Point")]
    [InlineData("Paid Rs.99 to Blinkit.", 9900, "Blinkit")]
    public void A_payment_app_paid_notification_becomes_a_debit(string text, long minor, string payee)
    {
        var parsed = Parse(AlertChannel.PaymentApp, GPay, text);

        Assert.NotNull(parsed);
        Assert.Equal(TransactionKind.Debit, parsed.Value.Direction);
        Assert.Equal(minor, parsed.Value.AmountMinor);
        Assert.Equal(payee, parsed.Value.Counterparty);
        Assert.Equal("GPay", parsed.Value.AppName);
        Assert.Equal(AlertChannel.PaymentApp, parsed.Value.Channel);
    }

    [Fact]
    public void A_note_after_for_is_kept_as_the_note()
    {
        var parsed = Parse(AlertChannel.PaymentApp, PhonePe, "Paid ₹600 to Rahul for Dinner at Toit");

        Assert.NotNull(parsed);
        Assert.Equal("Rahul", parsed.Value.Counterparty);
        Assert.Equal("Dinner at Toit", parsed.Value.Note);
        Assert.Equal("PhonePe", parsed.Value.AppName);
    }

    [Theory]
    [InlineData("Rahul paid you ₹500", "Rahul")]
    [InlineData("Received ₹500 from Rahul", "Rahul")]
    [InlineData("₹500 received from Rahul in your bank account", "Rahul")]
    public void A_payment_app_received_notification_becomes_a_credit(string text, string payer)
    {
        var parsed = Parse(AlertChannel.PaymentApp, Paytm, text);

        Assert.NotNull(parsed);
        Assert.Equal(TransactionKind.Credit, parsed.Value.Direction);
        Assert.Equal(50000, parsed.Value.AmountMinor);
        Assert.Equal(payer, parsed.Value.Counterparty);
    }

    [Fact]
    public void A_successful_payment_of_phrasing_is_read()
    {
        var parsed = Parse(AlertChannel.PaymentApp, Amazon, "Payment of ₹349 to Swiggy was successful");

        Assert.NotNull(parsed);
        Assert.Equal(34900, parsed.Value.AmountMinor);
        Assert.Equal("Swiggy", parsed.Value.Counterparty);
        Assert.Equal("Amazon Pay", parsed.Value.AppName);
    }

    [Theory]
    [InlineData("Payment of ₹450 to Zomato failed")]
    [InlineData("Payment of ₹450 to Zomato is pending")]
    [InlineData("Rahul requested ₹500 from you")]
    [InlineData("Pay ₹450 to Zomato to complete your order")]
    [InlineData("Get ₹50 cashback on your next bill payment")]
    [InlineData("₹450 sent to Zomato. Payment pending")]
    [InlineData("Paid ₹450 to Zomato\nTransaction declined by bank")]
    [InlineData("Received ₹500 from Rahul. Reversed")]
    public void Anything_that_is_not_a_completed_payment_produces_nothing(string text)
    {
        // A failed or requested payment parsed as a real one is exactly the wrong
        // suggestion FR-11.4 forbids.
        Assert.Null(Parse(AlertChannel.PaymentApp, GPay, text));
    }

    [Theory]
    [InlineData("Paid ₹5,000 to HDFC Credit Card", "HDFC Credit Card")]
    [InlineData("Payment of ₹12,340 for Axis Bank credit card bill was successful", "Axis Bank credit card bill")]
    [InlineData("₹2,000 paid towards ICICI Credit Card ending 4417", "ICICI Credit Card")]
    public void D1_a_card_bill_paid_through_an_app_is_a_self_transfer_not_spending(string text, string card)
    {
        var parsed = Parse(AlertChannel.PaymentApp, PaymentApps.Cred.Package, text);

        // Read as a Debit, it would count the card's purchases a second time on payment.
        Assert.NotNull(parsed);
        Assert.Equal(TransactionKind.SelfTransfer, parsed.Value.Direction);
        Assert.Equal(card, parsed.Value.Counterparty);
        Assert.Equal("CRED", parsed.Value.AppName);
    }

    [Fact]
    public void An_app_notification_is_never_read_by_an_sms_rule_or_the_reverse()
    {
        // The same words, through the wrong channel.
        Assert.Null(Parse(AlertChannel.Sms, GPay, "Paid ₹450 to Zomato"));
        Assert.Null(Parse(
            AlertChannel.PaymentApp,
            "VM-HDFCBK",
            "Rs.450.00 debited from a/c XX4417 on 30-08-26 to ZOMATO"));
    }

    [Fact]
    public void A_package_not_on_the_allow_list_is_not_read()
    {
        Assert.Null(Parse(AlertChannel.PaymentApp, "com.whatsapp", "Paid ₹450 to Zomato"));
    }

    [Fact]
    public void Hdfcs_line_per_fact_upi_format_is_read_with_its_date_and_card()
    {
        var parsed = Parse(
            AlertChannel.Sms,
            "AD-HDFCBK",
            "Sent Rs.450.00\nFrom HDFC Bank A/C *4417\nTo ZOMATO LTD\nOn 30/08/26\nRef 623412345678\nNot You?\nCall 18002586161");

        Assert.NotNull(parsed);
        Assert.Equal(45000, parsed.Value.AmountMinor);
        Assert.Equal("4417", parsed.Value.Last4);
        Assert.Equal("ZOMATO LTD", parsed.Value.Counterparty);
        Assert.Equal(new DateOnly(2026, 8, 30), parsed.Value.OccurredOn);
        Assert.Null(parsed.Value.AppName);
    }

    private static ParsedAlert? Parse(AlertChannel channel, string sender, string text)
    {
        var compiled = BuiltInParseRules.Create(DateTimeOffset.UnixEpoch)
            .Select(r => r.TryCompile(out var c, out _) ? c! : throw new InvalidOperationException(r.Name))
            .ToList();

        return AlertParser.Parse(channel, sender, new SmsBody(text), compiled, Currency.Inr);
    }
}
