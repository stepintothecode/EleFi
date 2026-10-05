using EleFi.Domain.Filters;

namespace EleFi.Ui.Services;

/// <summary>
/// What the All transactions screen was showing, kept while the user looks at one row.
/// </summary>
/// <remarks>
/// Opening a transaction and coming back used to land on a fresh, unfiltered list at the
/// top, so checking ten flagged transactions meant setting the filter up ten times. This
/// lives for the WebView's lifetime, so the list comes back as it was left: same filter,
/// same rows loaded, the row just visited scrolled to and marked.
/// </remarks>
public sealed class TransactionListState
{
    /// <summary>The filter the list was showing.</summary>
    public TransactionFilter Filter { get; set; } = TransactionFilter.Empty;

    /// <summary>The date preset behind the filter's dates, so its chip stays lit.</summary>
    public DatePreset Preset { get; set; } = DatePreset.AllTime;

    /// <summary>How many rows had been loaded, so paging does not start over.</summary>
    public int Loaded { get; set; }

    /// <summary>The row the user opened, to scroll back to and mark on return.</summary>
    public Guid? ReturnTo { get; set; }

    /// <summary>Forgets the row to return to, once it has been shown.</summary>
    /// <returns>The row that was remembered, if any.</returns>
    public Guid? TakeReturnTo()
    {
        var id = ReturnTo;
        ReturnTo = null;
        return id;
    }
}
