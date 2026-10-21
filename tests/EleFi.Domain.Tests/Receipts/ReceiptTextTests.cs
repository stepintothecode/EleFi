using EleFi.Domain.Money;
using EleFi.Domain.Receipts;

namespace EleFi.Domain.Tests.Receipts;

/// <summary>The small readings, each narrow enough to refuse rather than guess.</summary>
public class ReceiptTextTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Theory]
    [InlineData("₹450", 450_00)]
    [InlineData("Paid Rs. 1,240.50 to Gnani", 1_240_50)]
    [InlineData("INR 99", 99_00)]
    public void A_rupee_amount_is_read_with_its_sign(string line, long minor) =>
        Assert.Equal(minor, ReceiptText.Rupees(line, Currency.Inr));

    [Theory]
    [InlineData("Bill No 4521")]
    [InlineData("12 items")]
    public void A_bare_number_is_not_a_rupee_amount(string line) =>
        Assert.Null(ReceiptText.Rupees(line, Currency.Inr));

    [Theory]
    [InlineData("20/10/2026", 2026, 10, 20)]
    [InlineData("Date: 05-10-26", 2026, 10, 5)]
    [InlineData("19 Oct 2026, 7:12 pm", 2026, 10, 19)]
    [InlineData("on 3rd Oct, 2026", 2026, 10, 3)]
    [InlineData("Oct 18, 2026", 2026, 10, 18)]
    public void Dates_are_read_day_first_as_india_writes_them(string line, int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), ReceiptText.Date(line, Today));

    [Theory]
    [InlineData("31/02/2026")]
    [InlineData("Tel 080-2345-6789")]
    public void An_impossible_date_is_none(string line) => Assert.Null(ReceiptText.Date(line, Today));

    [Theory]
    [InlineData("7:12 pm", 19, 12)]
    [InlineData("12:05 AM", 0, 5)]
    [InlineData("18:57:03", 18, 57)]
    public void Times_are_read_in_both_clocks(string line, int hour, int minute) =>
        Assert.Equal(new TimeOnly(hour, minute), ReceiptText.Time(line));

    [Theory]
    [InlineData("XXXXXXXX4417", "4417")]
    [InlineData("HDFC Bank ••4417", "4417")]
    [InlineData("Card ending in 8648", "8648")]
    [InlineData("A/c XX1234 debited", "1234")]
    [InlineData("HDFC Bank 8411", "8411")]
    public void Only_an_accounts_last_four_digits_are_read(string line, string last4) =>
        Assert.Equal(last4, ReceiptText.Last4(line));

    [Theory]
    [InlineData("55", 55_00)]
    [InlineData("Z450", 450_00)]
    [InlineData("? 1,240.50", 1_240_50)]
    public void An_amount_alone_on_its_line_reads_even_without_its_rupee_sign(string line, long minor) =>
        Assert.Equal(minor, ReceiptText.Bare(line, Currency.Inr));

    [Theory]
    [InlineData("HDFC Bank 8411")]
    [InlineData("Bill No 4521")]
    [InlineData("20 Oct 2026")]
    public void A_number_among_words_is_not_a_bare_amount(string line) =>
        Assert.Null(ReceiptText.Bare(line, Currency.Inr));

    [Fact]
    public void An_amount_stated_in_words_reads() =>
        Assert.Equal(55_00, ReceiptText.Stated("n Payment of 55 completed", Currency.Inr));

    [Fact]
    public void A_reference_is_its_digits_however_they_are_spaced()
    {
        Assert.Equal("432167890123", ReceiptText.Reference("4321 6789 0123"));
        Assert.Null(ReceiptText.Reference("Ref 1234"));
    }

    [Theory]
    [InlineData("GNANI MART", "Gnani Mart")]
    [InlineData("Zomato Ltd", "Zomato Ltd")]
    public void A_shouted_name_is_tidied_and_a_written_one_kept(string read, string tidy) =>
        Assert.Equal(tidy, ReceiptText.Tidy(read));
}
