namespace EleFi.Domain.Receipts;

/// <summary>A piece of recognised text and where it sat on the image.</summary>
/// <param name="Text">The text.</param>
/// <param name="Left">Its left edge, in pixels.</param>
/// <param name="Top">Its top edge.</param>
/// <param name="Right">Its right edge.</param>
/// <param name="Bottom">Its bottom edge.</param>
public sealed record TextLine(string Text, int Left, int Top, int Right, int Bottom)
{
    /// <summary>Its vertical middle.</summary>
    public double Middle => (Top + Bottom) / 2.0;

    /// <summary>Its height.</summary>
    public int Height => Math.Max(1, Bottom - Top);
}

/// <summary>
/// Puts recognised text back into the rows it was printed in.
/// </summary>
/// <remarks>
/// Text recognition reads a receipt in blocks, and a receipt's columns often come back as
/// separate blocks: every item name, then every price. Read in that order, "Rice 5kg" and
/// "450.00" are pages apart. Rejoining pieces whose middles sit at the same height, left to
/// right, gives back the printed row, which is what the readers expect.
/// </remarks>
public static class TextRows
{
    /// <summary>The rows, top to bottom, each its pieces joined left to right.</summary>
    /// <param name="lines">Recognised pieces with their positions, in any order.</param>
    public static IReadOnlyList<string> Assemble(IEnumerable<TextLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var rows = new List<List<TextLine>>();
        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l.Text)).OrderBy(l => l.Middle))
        {
            // Same row when the middles are within half the shorter piece's height: tight
            // enough that two printed lines never merge, loose enough for a slightly tilted photo.
            var row = rows.LastOrDefault();
            if (row is not null && Math.Abs(row.Average(l => l.Middle) - line.Middle) <= Math.Min(row.Min(l => l.Height), line.Height) / 2.0)
            {
                row.Add(line);
            }
            else
            {
                rows.Add([line]);
            }
        }

        return [.. rows.Select(r => string.Join(' ', r.OrderBy(l => l.Left).Select(l => l.Text.Trim())))];
    }
}
