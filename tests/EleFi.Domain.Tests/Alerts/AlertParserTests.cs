using EleFi.Domain.Alerts;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Tests.Alerts;

/// <summary>
/// The SMS corpus. Real message shapes, redacted, plus the OTP set that no rule may ever
/// match.
/// </summary>
/// <remarks>
/// SM4 is the reason the OTP tests run against every rule rather than each rule's own
/// fixtures. Granting SMS access hands the app the user's second factor, and a rule that
/// happens to match an OTP would surface it.
/// </remarks>
public class AlertParserTests
{
    /// <summary>Real OTP shapes from Indian banks, with the codes changed.</summary>
    public static TheoryData<string> OtpMessages =>
    [
        "123456 is your OTP for txn of Rs.5000 at AMAZON. Do not share this OTP with anyone. -HDFC Bank",
        "OTP is 998877 for your ICICI Bank Credit Card ending 1234. Valid for 10 minutes. Never share.",
        "Use 445566 as one-time password to login. This OTP is valid for 5 min. Do not share with anyone.",
        "Your verification code is 778899. Do not share this code with anyone, including bank staff.",
        "Dear Customer, 112233 is the OTP for debit of Rs.2,500.00 from a/c XX4417. Do not share.",
    ];

    [Theory]
    [MemberData(nameof(OtpMessages))]
    public void SM4_no_rule_ever_parses_a_one_time_passcode(string message)
    {
        var rules = CompiledRules();

        // Every recognised sender is tried, because an OTP from a bank arrives from the
        // same header as its transaction alerts. Sender filtering alone would not stop it.
        foreach (var sender in new[] { "VM-HDFCBK", "AD-ICICIB", "JD-SBIBNK", "VK-AXISBK", "AX-KOTAKB" })
        {
            var parsed = AlertParser.Parse(sender, new SmsBody(message), rules, Currency.Inr);

            Assert.Null(parsed);
        }
    }

    [Theory]
    [MemberData(nameof(OtpMessages))]
    public void SM4_otp_detection_is_explicit_and_testable_on_its_own(string message)
    {
        Assert.True(AlertParser.LooksLikeOtp(new SmsBody(message)));
    }

    [Fact]
    public void An_hdfc_debit_alert_parses_into_the_facts_it_states()
    {
        var parsed = AlertParser.Parse(
            "VM-HDFCBK",
            new SmsBody("Rs.450.00 debited from a/c XX4417 on 30-08-26 to ZOMATO. Avl bal Rs.12,300.00"),
            CompiledRules(),
            Currency.Inr);

        Assert.NotNull(parsed);
        Assert.Equal(45000, parsed!.Value.AmountMinor);
        Assert.Equal("INR", parsed.Value.CurrencyCode);
        Assert.Equal(TransactionKind.Debit, parsed.Value.Direction);
        Assert.Equal("4417", parsed.Value.Last4);
        Assert.Contains("ZOMATO", parsed.Value.Counterparty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SM3_a_message_from_an_unrecognised_sender_is_not_parsed()
    {
        var parsed = AlertParser.Parse(
            "AX-PROMO",
            new SmsBody("Rs.450.00 debited from a/c XX4417 to ZOMATO"),
            CompiledRules(),
            Currency.Inr);

        // The sender is the first gate. A body that would otherwise match is never examined.
        Assert.Null(parsed);
    }

    [Fact]
    public void FR_11_4_a_matching_sender_with_an_unmatching_body_produces_nothing()
    {
        var parsed = AlertParser.Parse(
            "VM-HDFCBK",
            new SmsBody("Your account statement for August is now available in NetBanking."),
            CompiledRules(),
            Currency.Inr);

        // Not a partial guess with a missing amount. Nothing at all.
        Assert.Null(parsed);
    }

    [Fact]
    public void A_disabled_rule_is_not_used()
    {
        var rule = HdfcDebitRule();
        rule.IsEnabled = false;

        Assert.True(rule.TryCompile(out var compiled, out _));

        var parsed = AlertParser.Parse(
            "VM-HDFCBK",
            new SmsBody("Rs.450.00 debited from a/c XX4417 on 30-08-26 to ZOMATO"),
            [compiled!],
            Currency.Inr);

        Assert.Null(parsed);
    }

    [Fact]
    public void A_rule_without_an_amount_group_is_refused_at_compile_time()
    {
        var rule = new ParseRule
        {
            Name = "Broken",
            SenderPattern = "^VM-HDFCBK$",
            BodyPattern = @"debited from (?<last4>\d{4})",
        };

        // Refused while there is someone to tell, rather than silently matching nothing at
        // runtime.
        Assert.False(rule.TryCompile(out _, out var error));
        Assert.Contains("amount", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_rule_with_an_empty_sender_pattern_is_refused()
    {
        var rule = new ParseRule
        {
            Name = "Reads everything",
            SenderPattern = "",
            BodyPattern = @"(?<amount>\d+)",
        };

        // A rule matching every sender would read every message on the device.
        Assert.False(rule.TryCompile(out _, out var error));
        Assert.Contains("every sender", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SM6_the_same_alert_twice_produces_the_same_fingerprint()
    {
        var sent = new DateTimeOffset(2026, 8, 30, 14, 32, 10, TimeSpan.Zero);

        var first = CaptureSuggestion.ComputeFingerprint("VM-HDFCBK", sent, 45000, "4417");
        var again = CaptureSuggestion.ComputeFingerprint("VM-HDFCBK", sent.AddSeconds(25), 45000, "4417");

        // Truncated to the minute: a re-delivered alert is the same payment, and offering
        // the user two suggestions for one payment is worse than offering none.
        Assert.Equal(first, again);
    }

    [Fact]
    public void SM6_a_different_amount_is_a_different_payment()
    {
        var sent = new DateTimeOffset(2026, 8, 30, 14, 32, 10, TimeSpan.Zero);

        Assert.NotEqual(
            CaptureSuggestion.ComputeFingerprint("VM-HDFCBK", sent, 45000, "4417"),
            CaptureSuggestion.ComputeFingerprint("VM-HDFCBK", sent, 45001, "4417"));
    }

    private static ParseRule HdfcDebitRule() => new()
    {
        Name = "HDFC debit",
        SenderPattern = @"^[A-Z]{2}-HDFCBK(-[ST])?$",
        BodyPattern = @"(?<currency>Rs\.?|INR)\s*(?<amount>[\d,]+(?:\.\d{1,2})?)\s+debited\s+from\s+(?:a/c\s*)?(?:XX|\*+)?(?<last4>\d{4}).*?(?:to|at)\s+(?<counterparty>[A-Za-z0-9 &._-]{2,40})",
        Direction = TransactionKind.Debit,
        IsEnabled = true,
    };

    private static List<CompiledParseRule> CompiledRules()
    {
        var rules = new List<ParseRule>
        {
            HdfcDebitRule(),
            new()
            {
                Name = "ICICI debit",
                SenderPattern = @"^[A-Z]{2}-ICICIB(-[ST])?$",
                BodyPattern = @"(?<currency>Rs|INR)\s*(?<amount>[\d,]+(?:\.\d{1,2})?)\s+debited\s+from\s+(?:Acct\s*)?(?:XX|\*+)?(?<last4>\d{4})",
                Direction = TransactionKind.Debit,
                IsEnabled = true,
            },
            new()
            {
                Name = "SBI debit",
                SenderPattern = @"^[A-Z]{2}-SBIBNK(-[ST])?$",
                BodyPattern = @"(?<currency>Rs\.?|INR)\s*(?<amount>[\d,]+(?:\.\d{1,2})?)\s+(?:debited|withdrawn).*?(?:A/c\s*)?(?:XX|\*+)?(?<last4>\d{4})",
                Direction = TransactionKind.Debit,
                IsEnabled = true,
            },
        };

        var compiled = new List<CompiledParseRule>();
        foreach (var rule in rules)
        {
            Assert.True(rule.TryCompile(out var c, out var error), error);
            compiled.Add(c!);
        }

        return compiled;
    }
}
