using EleFi.Application.Suggestions;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Tests.Suggestions;

/// <summary>The words on the notification that says a payment was recorded.</summary>
public class AlertPromptTextTests
{
    private static readonly Party Card = new() { Kind = PartyKind.Container, ContainerId = Guid.NewGuid() };
    private static readonly Party Bank = new() { Kind = PartyKind.Container, ContainerId = Guid.NewGuid() };
    private static readonly Party Zomato = new() { Kind = PartyKind.External, Name = "Zomato" };

    [Fact]
    public void A_recorded_payment_says_where_it_went_and_that_it_needs_review()
    {
        var prompt = AlertPromptText.For(Spend(Card, Zomato, "Lunch"), "HDFC Card", "GPay");

        Assert.Equal("₹450.00 to Zomato", prompt.Title);
        Assert.Contains("From HDFC Card via GPay", prompt.Body, StringComparison.Ordinal);
        Assert.Contains("\"Lunch\"", prompt.Body, StringComparison.Ordinal);
        Assert.Contains("needs review", prompt.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Money_arriving_reads_as_from_and_into()
    {
        var prompt = AlertPromptText.For(Spend(Zomato, Bank, null), "SBI", null);

        Assert.Equal("₹450.00 from Zomato", prompt.Title);
        Assert.StartsWith("Into SBI.", prompt.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void A_card_bill_reads_as_a_card_bill()
    {
        var prompt = AlertPromptText.For(Spend(Bank, Card, null), "SBI", "CRED");

        Assert.Equal("₹450.00 card bill paid", prompt.Title);
    }

    [Fact]
    public void The_prompt_is_keyed_by_the_transaction_it_opens()
    {
        var transaction = Spend(Card, Zomato, null);

        Assert.Equal(transaction.Id, AlertPromptText.For(transaction, null, null).TransactionId);
    }

    private static Transaction Spend(Party source, Party destination, string? note) => new()
    {
        SourcePartyId = source.Id,
        SourceParty = source,
        DestinationPartyId = destination.Id,
        DestinationParty = destination,
        SourceAmountMinor = 45000,
        DestinationAmountMinor = 45000,
        Description = note,
    };
}
