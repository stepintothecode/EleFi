using EleFi.Application.Receipts;
using EleFi.Domain.Parties;
using EleFi.Domain.Receipts;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Tests.Receipts;

/// <summary>One wording for a discrepancy, short in a list and in full in the details.</summary>
public class DiscrepancyTextTests
{
    [Theory]
    [InlineData(Discrepancy.None, "Matches")]
    [InlineData(Discrepancy.Amount, "Amount differs")]
    [InlineData(Discrepancy.Payee, "Payee differs")]
    [InlineData(Discrepancy.Amount | Discrepancy.Account, "2 differences")]
    public void The_tag_is_a_few_words(Discrepancy found, string tag) => Assert.Equal(tag, DiscrepancyText.Tag(found));

    [Fact]
    public void Eles_summary_is_the_one_difference_in_full_or_names_several()
    {
        var shop = new Transaction
        {
            SourceParty = new Party { Kind = PartyKind.Container },
            DestinationParty = new Party { Kind = PartyKind.External, Name = "Gajanana Store" },
            SourceAmountMinor = 25_00,
        };
        var receipt = new ReceiptFacts("L.V.iyengar Bakery", 55_00, null, "8411");

        Assert.Equal("Amount: ₹55.00 on the receipt, ₹25.00 here.", DiscrepancyText.Summary(Discrepancy.Amount, receipt, shop));
        Assert.Equal(
            "Amount, payee and account differ from this one.",
            DiscrepancyText.Summary(Discrepancy.Amount | Discrepancy.Payee | Discrepancy.Account, receipt, shop));
    }

    [Fact]
    public void The_details_say_what_each_side_has()
    {
        var shop = new Transaction
        {
            SourceParty = new Party { Kind = PartyKind.Container },
            DestinationParty = new Party { Kind = PartyKind.External, Name = "Gajanana Store" },
            SourceAmountMinor = 500_00,
        };
        var receipt = new ReceiptFacts("Gnani Mart", 520_00, null, "8648");

        var lines = DiscrepancyText.Details(Discrepancy.Amount | Discrepancy.Payee | Discrepancy.Account, receipt, shop);

        Assert.Equal(
            [
                "Amount: ₹520.00 on the receipt, ₹500.00 here.",
                "Payee: Gnani Mart on the receipt, Gajanana Store here.",
                "Account: ending 8648 on the receipt, a different one here.",
            ],
            lines);
    }
}
