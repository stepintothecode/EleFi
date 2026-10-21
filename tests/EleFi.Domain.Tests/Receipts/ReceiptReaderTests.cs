using EleFi.Domain.Money;
using EleFi.Domain.Receipts;

namespace EleFi.Domain.Tests.Receipts;

/// <summary>
/// The corpus: one test per receipt shape, as text recognition returns it. A new shape that
/// reads wrongly is a new test here first.
/// </summary>
public class ReceiptReaderTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public void A_gpay_transaction_screen_reads_payee_amount_moment_reference_and_app()
    {
        string[] screen =
        [
            "To SRI SUNDHA MOBILES",
            "₹450",
            "✓ Completed",
            "20 Oct 2026, 7:12 pm",
            "HDFC Bank ••4417",
            "UPI transaction ID",
            "432167890123",
            "To: SRI SUNDHA MOBILES",
            "sundhamobiles@okaxis",
            "Google transaction ID",
            "CICAgKiQ8bS2Lw",
            "Powered by UPI | Google Pay",
        ];

        var reading = ReceiptReader.Read(screen, Currency.Inr, Today)!;

        Assert.Equal(ReceiptKind.PaymentApp, reading.Kind);
        Assert.Equal(new ReceiptFacts("Sri Sundha Mobiles", 450_00, Today, "4417"), reading.Facts);
        Assert.Equal(new TimeOnly(19, 12), reading.OccurredAtTime);
        Assert.Equal("432167890123", reading.Reference);
        Assert.Equal("GPay", reading.AppName);
        Assert.Empty(reading.LineItems);
    }

    [Fact]
    public void A_real_gpay_screen_read_on_a_phone_with_the_rupee_sign_dropped_still_reads()
    {
        // Exactly what ML Kit read from a real GPay screen on 2026-10-21, names changed. The ₹
        // before 55 did not survive, the account is written plainly, and the note sits
        // unlabelled under the amount.
        string[] screen =
        [
            "L",
            "To L.V.iyengar Bakery",
            "55",
            "buns",
            "Completed",
            "20 Oct 2026, 6:46 pm",
            "HDFC Bank 8411",
            "n Payment of 55 completed",
            "Receiver's bank has confirmed deposit",
            "of money to L.V.iyengar Bakery 's bank",
            "account",
            "UPI transaction ID",
            "131604324342",
            "To: L V IYENGAR BAKERY",
            "...6067@okbizaxis",
            "From: A PAYER (HDFC Bank)",
            "***er-3@okhdfcbank on Google Pay",
            "Google transaction ID",
            "CICAgPirgkroNA",
            "UPI",
            "G Pay",
        ];

        var reading = ReceiptReader.Read(screen, Currency.Inr, Today)!;

        Assert.Equal(ReceiptKind.PaymentApp, reading.Kind);
        Assert.Equal(new ReceiptFacts("L.V.iyengar Bakery", 55_00, Today, "8411"), reading.Facts);
        Assert.Equal(new TimeOnly(18, 46), reading.OccurredAtTime);
        Assert.Equal("131604324342", reading.Reference);
        Assert.Equal("buns", reading.Note);
        Assert.Equal("GPay", reading.AppName);
    }

    [Fact]
    public void A_phonepe_screen_reads_the_payee_under_its_label_and_prefers_the_utr()
    {
        string[] screen =
        [
            "Transaction Successful",
            "08:45 pm on 19 Oct 2026",
            "Paid to",
            "Zomato Ltd",
            "₹ 312",
            "Transfer Details",
            "Transaction ID",
            "T2610192045123456789",
            "Debited from",
            "XXXXXXXX4417 ₹312",
            "UTR: 123456789012",
            "PhonePe",
        ];

        var reading = ReceiptReader.Read(screen, Currency.Inr, Today)!;

        Assert.Equal(new ReceiptFacts("Zomato Ltd", 312_00, new DateOnly(2026, 10, 19), "4417"), reading.Facts);
        Assert.Equal(new TimeOnly(20, 45), reading.OccurredAtTime);
        Assert.Equal("123456789012", reading.Reference);
        Assert.Equal("PhonePe", reading.AppName);
    }

    [Fact]
    public void A_note_on_a_payment_screen_is_kept()
    {
        string[] screen = ["Paid to", "Mom", "₹2,000", "Note: Birthday", "UPI Ref No 998877665544"];

        var reading = ReceiptReader.Read(screen, Currency.Inr, Today)!;

        Assert.Equal("Birthday", reading.Note);
        Assert.Equal("998877665544", reading.Reference);
        Assert.Equal(2_000_00, reading.Facts.AmountMinor);
    }

    [Fact]
    public void A_shop_receipt_reads_the_name_the_grand_total_the_date_and_its_rows()
    {
        string[] receipt =
        [
            "GNANI MART",
            "#12, 4th Cross, Jayanagar",
            "GSTIN: 29ABCDE1234F1Z5",
            "TAX INVOICE",
            "Bill No: 4521 Date: 20/10/2026 18:57",
            "Item Qty Amount",
            "Sona Masoori Rice 5kg 1 450.00",
            "Sunflower Oil 1L 1 180.00",
            "Toor Dal 1kg 1 165.00",
            "Tomato 1 60.00",
            "Biscuits 2 40.00",
            "Sub Total 895.00",
            "CGST 2.5% 11.19",
            "SGST 2.5% 11.19",
            "Grand Total 917.38",
            "Cash 1000.00",
            "Change 82.62",
        ];

        var reading = ReceiptReader.Read(receipt, Currency.Inr, Today)!;

        Assert.Equal(ReceiptKind.Shop, reading.Kind);
        Assert.Equal(new ReceiptFacts("Gnani Mart", 917_38, Today, null), reading.Facts);
        Assert.Equal(new TimeOnly(18, 57), reading.OccurredAtTime);
        Assert.Equal(
            [
                new ReadItem("Sona Masoori Rice 5kg", 450_00),
                new ReadItem("Sunflower Oil 1L", 180_00),
                new ReadItem("Toor Dal 1kg", 165_00),
                new ReadItem("Tomato", 60_00),
                new ReadItem("Biscuits", 40_00),
            ],
            reading.LineItems);
    }

    [Fact]
    public void A_receipt_with_no_total_line_goes_by_its_largest_amount()
    {
        string[] receipt = ["Chai Point", "Masala chai 40.00", "Bun maska 60.00", "100.00"];

        var reading = ReceiptReader.Read(receipt, Currency.Inr, Today)!;

        Assert.Equal(100_00, reading.Facts.AmountMinor);
        Assert.Equal("Chai Point", reading.Facts.Payee);
    }

    [Fact]
    public void Text_with_no_amount_is_not_a_receipt()
    {
        Assert.Null(ReceiptReader.Read(["Hello", "Nothing to see"], Currency.Inr, Today));
    }

    [Fact]
    public void A_date_in_the_future_is_a_misreading_and_is_left_out()
    {
        var reading = ReceiptReader.Read(["Shop", "Date 21/10/2026", "Total 50.00"], Currency.Inr, Today)!;

        Assert.Null(reading.Facts.OccurredOn);
    }
}
