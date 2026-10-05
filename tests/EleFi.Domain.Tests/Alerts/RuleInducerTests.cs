using EleFi.Domain.Alerts;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Tests.Alerts;

/// <summary>Teaching EleFi a message format from one annotated example.</summary>
public class RuleInducerTests
{
    private const string Taught = "Rs.450.00 debited from a/c XX4417 on 30-08-26 to ZOMATO. Avl bal Rs.12,300.00";

    private static readonly TeachingExample Example = new(
        "VM-HDFCBK", Taught, TransactionKind.Debit, "450.00", "ZOMATO", "4417", "30-08-26");

    [Fact]
    public void A_taught_rule_reads_its_own_example_back()
    {
        var taught = RuleInducer.Induce(Example, DateTimeOffset.UnixEpoch);

        Assert.True(taught.Succeeded, taught.Error);
        var parsed = Parse(taught.Rule!, "VM-HDFCBK", Taught);

        Assert.Equal(45000, parsed!.Value.AmountMinor);
        Assert.Equal("ZOMATO", parsed.Value.Counterparty);
        Assert.Equal("4417", parsed.Value.Last4);
        Assert.Equal(new DateOnly(2026, 8, 30), parsed.Value.OccurredOn);
    }

    [Fact]
    public void It_reads_the_next_message_of_the_same_kind_with_different_values()
    {
        var rule = RuleInducer.Induce(Example, DateTimeOffset.UnixEpoch).Rule!;

        var parsed = Parse(
            rule,
            "AD-HDFCBK",
            "Rs.1,250.50 debited from a/c XX4417 on 01-09-26 to BIG BAZAAR RETAIL. Avl bal Rs.9,04,000.00");

        // A different amount, payee, date, balance and route prefix: the same template.
        Assert.NotNull(parsed);
        Assert.Equal(125050, parsed.Value.AmountMinor);
        Assert.Equal("BIG BAZAAR RETAIL", parsed.Value.Counterparty);
        Assert.Equal(TransactionKind.Debit, parsed.Value.Direction);
    }

    [Fact]
    public void It_does_not_read_a_different_sender()
    {
        var rule = RuleInducer.Induce(Example, DateTimeOffset.UnixEpoch).Rule!;

        Assert.Null(Parse(rule, "VM-ICICIB", Taught));
    }

    [Fact]
    public void A_taught_rule_is_the_users_own_and_runs_ahead_of_the_built_ins()
    {
        var rule = RuleInducer.Induce(Example, DateTimeOffset.UnixEpoch).Rule!;

        Assert.False(rule.IsBuiltIn);
        Assert.True(rule.IsEnabled);
        Assert.Equal(AlertChannel.Sms, rule.Channel);
        Assert.True(rule.Priority < BuiltInParseRules.All.Min(r => r.Priority));
        Assert.Contains("HDFCBK", rule.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_that_is_not_in_the_message_is_refused_with_a_reason()
    {
        var taught = RuleInducer.Induce(Example with { Counterparty = "Zomato Ltd" }, DateTimeOffset.UnixEpoch);

        Assert.False(taught.Succeeded);
        Assert.Contains("Zomato Ltd", taught.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void SM4_an_otp_message_can_never_be_taught()
    {
        var taught = RuleInducer.Induce(
            Example with { Message = "123456 is your OTP for txn of Rs.450.00. Do not share.", Amount = "450.00", Counterparty = null, Last4 = null, OccurredOn = null },
            DateTimeOffset.UnixEpoch);

        Assert.False(taught.Succeeded);
        Assert.Contains("passcode", taught.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("lots")]
    public void The_amount_must_be_a_real_amount(string amount)
    {
        Assert.False(RuleInducer.Induce(Example with { Amount = amount }, DateTimeOffset.UnixEpoch).Succeeded);
    }

    [Fact]
    public void The_account_must_be_four_digits()
    {
        Assert.False(RuleInducer.Induce(Example with { Last4 = "XX4417" }, DateTimeOffset.UnixEpoch).Succeeded);
    }

    [Fact]
    public void Only_the_amount_is_required()
    {
        var taught = RuleInducer.Induce(
            new TeachingExample("JD-SBIBNK", "Your a/c is credited with INR 2,000.00 on 05/10/26.", TransactionKind.Credit, "2,000.00"),
            DateTimeOffset.UnixEpoch);

        Assert.True(taught.Succeeded, taught.Error);
        Assert.Equal(TransactionKind.Credit, taught.Rule!.Direction);
    }

    [Fact]
    public void A_payee_at_the_very_end_of_the_message_is_read_to_the_end()
    {
        var taught = RuleInducer.Induce(
            new TeachingExample("VM-KOTAKB", "Sent Rs 99 to Chai Point", TransactionKind.Debit, "99", "Chai Point"),
            DateTimeOffset.UnixEpoch);

        Assert.True(taught.Succeeded, taught.Error);
        Assert.Equal("Rahul Kumar", Parse(taught.Rule!, "VM-KOTAKB", "Sent Rs 500 to Rahul Kumar")!.Value.Counterparty);
    }

    private static ParsedAlert? Parse(ParseRule rule, string sender, string text)
    {
        Assert.True(rule.TryCompile(out var compiled, out var error), error);
        return AlertParser.Parse(sender, new SmsBody(text), [compiled!], Currency.Inr);
    }
}
