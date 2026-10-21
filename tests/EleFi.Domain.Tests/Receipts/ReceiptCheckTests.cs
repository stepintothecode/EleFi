using EleFi.Domain.Parties;
using EleFi.Domain.Receipts;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Tests.Receipts;

/// <summary>A receipt disagrees with its transaction only on what the receipt actually shows.</summary>
public class ReceiptCheckTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public void A_receipt_that_agrees_has_no_discrepancy()
    {
        var facts = new ReceiptFacts("SRI SUNDHA MOBILES", 450_00, Today, "4417");

        Assert.Equal(Discrepancy.None, ReceiptCheck.Compare(facts, Paid("Sri Sundha Mobiles", 450_00), "4417"));
    }

    [Fact]
    public void Each_disagreement_is_its_own_flag()
    {
        var facts = new ReceiptFacts("Gnani Mart", 520_00, Today, "8648");

        var found = ReceiptCheck.Compare(facts, Paid("Gajanana Store", 500_00), "4417");

        Assert.Equal(Discrepancy.Amount | Discrepancy.Payee | Discrepancy.Account, found);
    }

    [Fact]
    public void What_the_receipt_does_not_show_cannot_disagree()
    {
        var facts = new ReceiptFacts(null, null, null, null);

        Assert.Equal(Discrepancy.None, ReceiptCheck.Compare(facts, Paid("Anyone", 1_00), "4417"));
    }

    [Fact]
    public void A_payee_known_by_another_name_through_an_alias_agrees()
    {
        var facts = new ReceiptFacts("BigBasket Innovative Retail", 500_00, Today, null);

        Assert.Equal(Discrepancy.None, ReceiptCheck.Compare(facts, Paid("BB", 500_00), null, ["BigBasket"]));
    }

    [Fact]
    public void A_credit_is_checked_against_who_paid()
    {
        var mom = new Party { Kind = PartyKind.External, Name = "Mom" };
        var bank = new Party { Kind = PartyKind.Container };
        var credit = new Transaction { SourceParty = mom, DestinationParty = bank, SourceAmountMinor = 2_000_00 };

        Assert.Equal("Mom", ReceiptCheck.Counterparty(credit));
        Assert.Equal(Discrepancy.None, ReceiptCheck.Compare(new ReceiptFacts("Mom", 2_000_00, Today, null), credit, null));
    }

    internal static Transaction Paid(string payee, long minor, DateOnly? on = null, TimeOnly? at = null) => new()
    {
        SourceParty = new Party { Kind = PartyKind.Container },
        DestinationParty = new Party { Kind = PartyKind.External, Name = payee },
        SourceAmountMinor = minor,
        DestinationAmountMinor = minor,
        OccurredOn = on ?? Today,
        OccurredAtTime = at,
    };
}
