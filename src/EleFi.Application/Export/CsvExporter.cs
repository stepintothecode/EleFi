using System.Globalization;
using System.Text;
using EleFi.Application.Abstractions;
using EleFi.Domain.Filters;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Export;

/// <summary>A column the export can write.</summary>
public enum ExportColumn
{
    /// <summary>The calendar day, ISO-8601.</summary>
    Date = 0,

    /// <summary>Debit, Credit, or Self Transfer.</summary>
    Kind = 1,

    /// <summary>The source party's name.</summary>
    Source = 2,

    /// <summary>The destination party's name.</summary>
    Destination = 3,

    /// <summary>What left the source, as a bare decimal.</summary>
    SourceAmount = 4,

    /// <summary>The source currency code.</summary>
    SourceCurrency = 5,

    /// <summary>What arrived, as a bare decimal.</summary>
    DestinationAmount = 6,

    /// <summary>The destination currency code.</summary>
    DestinationCurrency = 7,

    /// <summary>All labels on the transaction, semicolon separated.</summary>
    Labels = 8,

    /// <summary>The time of day, when one was recorded.</summary>
    Time = 9,

    /// <summary>Free text.</summary>
    Description = 10,

    /// <summary>The platform bought through.</summary>
    MarketplaceApp = 11,

    /// <summary>The rail money moved along.</summary>
    PaymentApp = 12,

    /// <summary>Whether the values were guessed.</summary>
    NeedsReview = 13,

    /// <summary>How it was recorded.</summary>
    CaptureSource = 14,

    /// <summary>When the record was created.</summary>
    CreatedAt = 15,

    /// <summary>When the record last changed.</summary>
    UpdatedAt = 16,
}

/// <summary>A CSV file, ready to hand to the OS.</summary>
/// <param name="FileName">The suggested name, encoding the filter that produced it.</param>
/// <param name="Contents">UTF-8 with a BOM.</param>
/// <param name="RowCount">How many transactions it contains.</param>
public readonly record struct CsvExport(string FileName, ReadOnlyMemory<byte> Contents, int RowCount);

/// <summary>
/// Serialises the filtered transaction list to CSV.
/// </summary>
/// <remarks>
/// <para>
/// <b>The export is the filtered view, serialised (X1).</b> This takes a
/// <see cref="TransactionFilter"/> and hands it to the same repository query the list
/// uses. There is no second query, no separate export configuration, and no way for the
/// two to disagree, because there is only one of them.
/// </para>
/// <para>
/// The empty filter means everything (X2), so an unfiltered export contains every
/// transaction the user could reach by scrolling.
/// </para>
/// </remarks>
public sealed class CsvExporter(ITransactionRepository transactions, IClock clock)
{
    /// <summary>The columns written when the user has not chosen a subset.</summary>
    public static IReadOnlyList<ExportColumn> DefaultColumns { get; } =
        Enum.GetValues<ExportColumn>().ToArray();

    /// <summary>
    /// Builds the CSV for a filter.
    /// </summary>
    /// <remarks>
    /// UTF-8 <b>with a BOM</b> (FR-7.3), because without it Excel renders the rupee sign
    /// and any Devanagari as mojibake, and the first thing the user does with their export
    /// is spend twenty minutes fixing the encoding.
    /// </remarks>
    /// <param name="filter">The filter currently applied to the list.</param>
    /// <param name="columns">Columns to write, or null for all of them.</param>
    /// <param name="containerNames">Names of any selected containers, for the filename.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<CsvExport> ExportAsync(
        TransactionFilter filter,
        IReadOnlyList<ExportColumn>? columns = null,
        IReadOnlyList<string>? containerNames = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var selected = columns is { Count: > 0 } ? columns : DefaultColumns;

        // The same call the list makes. Passing no take means every matching row, in the
        // same order the list shows them.
        var rows = await transactions.QueryAsync(filter, 0, null, cancellationToken).ConfigureAwait(false);

        var csv = new StringBuilder();
        csv.Append(string.Join(',', selected.Select(c => Escape(HeaderFor(c))))).Append("\r\n");

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            csv.Append(string.Join(',', selected.Select(c => Escape(ValueFor(row, c))))).Append("\r\n");
        }

        var bytes = new byte[Encoding.UTF8.GetPreamble().Length + Encoding.UTF8.GetByteCount(csv.ToString())];
        Encoding.UTF8.GetPreamble().CopyTo(bytes, 0);
        Encoding.UTF8.GetBytes(csv.ToString(), 0, csv.Length, bytes, Encoding.UTF8.GetPreamble().Length);

        var slug = filter.ToFilenameSlug(containerNames);
        var stamp = clock.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return new CsvExport($"elefi-{slug}-{stamp}.csv", bytes, rows.Count);
    }

    /// <summary>The header text for a column.</summary>
    /// <param name="column">The column.</param>
    public static string HeaderFor(ExportColumn column) => column switch
    {
        ExportColumn.Date => "Date",
        ExportColumn.Kind => "Kind",
        ExportColumn.Source => "Source",
        ExportColumn.Destination => "Destination",
        ExportColumn.SourceAmount => "Source Amount",
        ExportColumn.SourceCurrency => "Source Currency",
        ExportColumn.DestinationAmount => "Destination Amount",
        ExportColumn.DestinationCurrency => "Destination Currency",
        ExportColumn.Labels => "Labels",
        ExportColumn.Time => "Time",
        ExportColumn.Description => "Description",
        ExportColumn.MarketplaceApp => "Marketplace App",
        ExportColumn.PaymentApp => "Payment App",
        ExportColumn.NeedsReview => "Needs Review",
        ExportColumn.CaptureSource => "Capture Source",
        ExportColumn.CreatedAt => "Created At",
        ExportColumn.UpdatedAt => "Updated At",
        _ => column.ToString(),
    };

    private static string ValueFor(Transaction row, ExportColumn column) => column switch
    {
        // ISO-8601 (FR-7.4): unambiguous, sortable as text, and the only format that does
        // not depend on whether the reader is in India or the United States.
        ExportColumn.Date => row.OccurredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ExportColumn.Kind => row.Kind.DisplayName(),
        ExportColumn.Source => NameOf(row.SourceParty),
        ExportColumn.Destination => NameOf(row.DestinationParty),

        // Bare decimals, no symbol, no grouping (FR-7.5), so a spreadsheet treats the
        // column as numbers and the user can sum it without cleaning it first.
        ExportColumn.SourceAmount => MoneyText.ToBareDecimal(row.SourceAmount),
        ExportColumn.SourceCurrency => row.SourceCurrencyCode,
        ExportColumn.DestinationAmount => MoneyText.ToBareDecimal(row.DestinationAmount),
        ExportColumn.DestinationCurrency => row.DestinationCurrencyCode,

        // Semicolon separated, because a transaction now carries several and a comma would
        // need the whole field quoting on almost every row.
        ExportColumn.Labels => string.Join(
            ';',
            row.Labels.Select(l => l.Label?.Name).Where(n => n is not null).Order(StringComparer.Ordinal)),

        // Blank rather than a made-up midnight when no time was recorded. A transaction
        // happened on a date; the time is extra, and inventing one puts a fact in the file
        // the user never supplied.
        ExportColumn.Time => row.OccurredAtTime?.ToString("HH\\:mm", CultureInfo.InvariantCulture) ?? string.Empty,
        ExportColumn.Description => row.Description ?? string.Empty,
        ExportColumn.MarketplaceApp => row.MarketplaceApp?.Name ?? string.Empty,
        ExportColumn.PaymentApp => row.PaymentApp?.Name ?? string.Empty,
        ExportColumn.NeedsReview => row.NeedsReview ? "yes" : "no",
        ExportColumn.CaptureSource => row.CaptureSource.ToString(),
        ExportColumn.CreatedAt => row.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        ExportColumn.UpdatedAt => row.UpdatedAt.ToString("O", CultureInfo.InvariantCulture),
        _ => string.Empty,
    };

    private static string NameOf(Domain.Parties.Party? party) =>
        party is null ? string.Empty : party.Name ?? "(container)";

    // RFC 4180: quote when the value contains a comma, a quote, or a newline, and double
    // any quote inside. A description with a comma in it is not an exotic case.
    private static string Escape(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var needsQuotes = value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal);

        return needsQuotes
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}
