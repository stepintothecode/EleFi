using EleFi.Application.Suggestions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Tests.Suggestions;

/// <summary>The words on a Suggestion Prompt.</summary>
public class SuggestionPromptTextTests
{
    [Fact]
    public void SM2_a_prompt_asks_and_says_nothing_has_been_recorded()
    {
        var prompt = SuggestionPromptText.For(Suggestion(TransactionKind.Debit, "Zomato"), "HDFC Card");

        Assert.EndsWith("?", prompt.Title, StringComparison.Ordinal);
        Assert.Contains("Zomato", prompt.Title, StringComparison.Ordinal);
        Assert.Contains("Not recorded yet", prompt.Body, StringComparison.Ordinal);
        Assert.Contains("From HDFC Card", prompt.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Money_arriving_reads_as_from_and_into()
    {
        var prompt = SuggestionPromptText.For(Suggestion(TransactionKind.Credit, "Rahul"), "SBI");

        Assert.Contains("from Rahul", prompt.Title, StringComparison.Ordinal);
        Assert.Contains("Into SBI", prompt.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void The_app_and_the_note_are_mentioned_when_known()
    {
        var suggestion = Suggestion(TransactionKind.Debit, "Rahul");
        suggestion.PaymentAppName = "GPay";
        suggestion.Note = "Dinner";

        var prompt = SuggestionPromptText.For(suggestion, null);

        Assert.Contains("via GPay", prompt.Body, StringComparison.Ordinal);
        Assert.Contains("\"Dinner\"", prompt.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void A_card_bill_reads_as_paying_your_own_card()
    {
        var prompt = SuggestionPromptText.For(Suggestion(TransactionKind.SelfTransfer, "HDFC Credit Card"), "SBI");

        Assert.Contains("paid to your HDFC Credit Card?", prompt.Title, StringComparison.Ordinal);
        Assert.Contains("From SBI", prompt.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_counterparty_the_title_still_reads_as_a_question()
    {
        var prompt = SuggestionPromptText.For(Suggestion(TransactionKind.Debit, null), null);

        Assert.EndsWith("spent?", prompt.Title, StringComparison.Ordinal);
    }

    private static CaptureSuggestion Suggestion(TransactionKind direction, string? who) => new()
    {
        AmountMinor = 45000,
        CurrencyCode = "INR",
        Direction = direction,
        CounterpartyText = who,
    };
}
