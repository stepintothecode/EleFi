using EleFi.Domain.Receipts;

namespace EleFi.Domain.Tests.Receipts;

/// <summary>Columns read as separate blocks are put back into printed rows.</summary>
public class TextRowsTests
{
    [Fact]
    public void Names_and_prices_read_as_two_columns_come_back_as_rows()
    {
        TextLine[] read =
        [
            new("Rice 5kg", 10, 100, 200, 130),
            new("Oil 1L", 10, 140, 180, 170),
            new("450.00", 400, 102, 480, 131),
            new("180.00", 400, 141, 480, 169),
        ];

        Assert.Equal(["Rice 5kg 450.00", "Oil 1L 180.00"], TextRows.Assemble(read));
    }

    [Fact]
    public void Lines_close_together_but_not_level_stay_apart()
    {
        TextLine[] read = [new("Total", 10, 100, 100, 120), new("Cash", 10, 122, 100, 142)];

        Assert.Equal(["Total", "Cash"], TextRows.Assemble(read));
    }

    [Fact]
    public void Blank_pieces_are_dropped() =>
        Assert.Equal(["Total"], TextRows.Assemble([new("  ", 0, 0, 10, 10), new("Total", 0, 50, 10, 60)]));
}
