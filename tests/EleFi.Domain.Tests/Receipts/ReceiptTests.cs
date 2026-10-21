using EleFi.Domain.Receipts;

namespace EleFi.Domain.Tests.Receipts;

/// <summary>A receipt keeps what was read, as read, cut to what the columns hold.</summary>
public class ReceiptTests
{
    [Fact]
    public void A_receipt_keeps_everything_read_and_belongs_to_its_transaction()
    {
        var transaction = Guid.NewGuid();
        var reading = new ReceiptReading(
            ReceiptKind.PaymentApp,
            new ReceiptFacts("Zomato", 312_00, new DateOnly(2026, 10, 19), "4417"),
            new TimeOnly(20, 45),
            "PhonePe",
            "123456789012",
            "Lunch");

        var receipt = Receipt.From(reading, transaction);

        Assert.Equal(transaction, receipt.TransactionId);
        Assert.Equal(ReceiptKind.PaymentApp, receipt.Kind);
        Assert.Equal(reading.Facts, receipt.Facts);
        Assert.Equal(("PhonePe", "123456789012", "Lunch", new TimeOnly(20, 45)), (receipt.AppName, receipt.Reference, receipt.Note, receipt.OccurredAtTime));
        Assert.Null(receipt.SettledAt);
    }

    [Fact]
    public void Text_longer_than_its_column_is_cut_rather_than_refused()
    {
        var reading = new ReceiptReading(ReceiptKind.Shop, new ReceiptFacts(new string('A', 300), 1_00, null, null), Note: new string('n', 250));

        var receipt = Receipt.From(reading, Guid.NewGuid());

        Assert.Equal(Receipt.MaxPayeeLength, receipt.Payee!.Length);
        Assert.Equal(Receipt.MaxNoteLength, receipt.Note!.Length);
    }
}
