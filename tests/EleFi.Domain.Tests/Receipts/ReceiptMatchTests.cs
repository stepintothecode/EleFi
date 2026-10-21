using EleFi.Domain.Receipts;

namespace EleFi.Domain.Tests.Receipts;

/// <summary>Which transaction a receipt belongs to, or none.</summary>
public class ReceiptMatchTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public void The_same_amount_on_the_same_day_is_the_one()
    {
        var lunch = ReceiptCheckTests.Paid("Zomato", 312_00);
        var other = ReceiptCheckTests.Paid("Swiggy", 250_00);

        Assert.Same(lunch, ReceiptMatch.Best(Reading("Zomato Ltd", 312_00, Today), [other, lunch]));
    }

    [Fact]
    public void The_right_payee_on_the_right_day_matches_even_with_the_amount_wrong()
    {
        var shop = ReceiptCheckTests.Paid("Sri Sundha Mobiles", 300_00);

        // This is the receipt that should arrive with a discrepancy, not as a new transaction.
        Assert.Same(shop, ReceiptMatch.Best(Reading("SRI SUNDHA MOBILES", 450_00, Today), [shop]));
    }

    [Fact]
    public void Nothing_close_enough_is_no_match()
    {
        var shop = ReceiptCheckTests.Paid("Gnani Mart", 900_00);

        Assert.Null(ReceiptMatch.Best(Reading("Chai Point", 100_00, Today), [shop]));
    }

    [Fact]
    public void A_transaction_more_than_three_days_away_is_never_the_one()
    {
        var old = ReceiptCheckTests.Paid("Zomato", 312_00, Today.AddDays(-4));

        Assert.Null(ReceiptMatch.Best(Reading("Zomato", 312_00, Today), [old]));
    }

    [Fact]
    public void Of_two_equal_amounts_the_one_at_the_receipts_time_wins()
    {
        var morning = ReceiptCheckTests.Paid("Zomato", 312_00, at: new TimeOnly(9, 0));
        var evening = ReceiptCheckTests.Paid("Zomato", 312_00, at: new TimeOnly(20, 40));
        var reading = Reading("Zomato", 312_00, Today) with { OccurredAtTime = new TimeOnly(20, 45) };

        Assert.Same(evening, ReceiptMatch.Best(reading, [morning, evening]));
    }

    private static ReceiptReading Reading(string payee, long minor, DateOnly on) =>
        new(ReceiptKind.PaymentApp, new ReceiptFacts(payee, minor, on, null));
}
