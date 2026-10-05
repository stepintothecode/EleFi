using EleFi.Application.Abstractions;
using EleFi.Application.Export;
using EleFi.Domain.Balances;
using EleFi.Domain.Filters;
using EleFi.Domain.Labels;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Tests.Export;

/// <summary>
/// CSV shape, with the repository faked.
/// </summary>
/// <remarks>
/// The fake is the point: it proves the exporter asks the repository for the filter it was
/// handed and serialises whatever comes back. Everything about whether the <em>query</em>
/// is right is tested against a real database in EleFi.Infrastructure.Tests, where it
/// belongs.
/// </remarks>
public class CsvExporterTests
{
    private static readonly DateOnly Today = new(2026, 8, 30);

    [Fact]
    public async Task The_exporter_passes_the_filter_through_untouched()
    {
        var repo = new RecordingRepository();
        var exporter = new CsvExporter(repo, new StoppedClock(Today));

        var filter = new TransactionFilter { Search = "zomato", NeedsReview = true };
        await exporter.ExportAsync(filter);

        // X1 depends on the exporter not "helpfully" adjusting the filter on the way past.
        Assert.Same(filter, repo.LastFilter);

        // No take: every matching row, not the first page the list happened to load.
        Assert.Null(repo.LastTake);
    }

    [Fact]
    public async Task Values_containing_commas_or_quotes_are_escaped_to_rfc4180()
    {
        var repo = new RecordingRepository
        {
            Rows = [Row("Shop, Inc \"the best\"", 45050, "Line one\nline two")],
        };

        var exporter = new CsvExporter(repo, new StoppedClock(Today));
        var export = await exporter.ExportAsync(TransactionFilter.Empty);
        var csv = Text(export);

        // A description with a comma in it is not an exotic case, and getting this wrong
        // shifts every later column by one.
        Assert.Contains("\"Shop, Inc \"\"the best\"\"\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Line one\nline two\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Amounts_are_bare_decimals_a_spreadsheet_can_sum()
    {
        var repo = new RecordingRepository { Rows = [Row("Zomato", 45050)] };
        var exporter = new CsvExporter(repo, new StoppedClock(Today));

        var export = await exporter.ExportAsync(
            TransactionFilter.Empty,
            [ExportColumn.SourceAmount, ExportColumn.SourceCurrency]);

        var lines = Text(export).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Source Amount,Source Currency", lines[0]);

        // No symbol, no grouping (FR-7.5). "450.50", not "₹450.50".
        Assert.Equal("450.50,INR", lines[1]);
    }

    [Fact]
    public async Task Every_label_on_a_row_reaches_the_file_semicolon_separated()
    {
        var row = Row("Big Bazaar", 200000);
        row.Labels.Add(new TransactionLabel { Label = new Label { Name = "Household" } });
        row.Labels.Add(new TransactionLabel { Label = new Label { Name = "Food" } });

        var repo = new RecordingRepository { Rows = [row] };
        var exporter = new CsvExporter(repo, new StoppedClock(Today));

        var export = await exporter.ExportAsync(TransactionFilter.Empty, [ExportColumn.Labels]);
        var lines = Text(export).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        // Semicolons, not commas: a comma would quote the field on almost every row. Sorted,
        // so two exports of the same data are comparable regardless of attachment order.
        Assert.Equal("Food;Household", lines[1]);
    }

    [Fact]
    public async Task A_row_with_no_time_leaves_the_column_blank_rather_than_inventing_midnight()
    {
        var repo = new RecordingRepository { Rows = [Row("Zomato", 10000)] };
        var exporter = new CsvExporter(repo, new StoppedClock(Today));

        var export = await exporter.ExportAsync(
            TransactionFilter.Empty, [ExportColumn.Date, ExportColumn.Time]);

        var lines = Text(export).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("2026-08-30,", lines[1]);
    }

    [Fact]
    public async Task A_chosen_column_subset_is_respected_in_order()
    {
        var repo = new RecordingRepository { Rows = [Row("Zomato", 10000)] };
        var exporter = new CsvExporter(repo, new StoppedClock(Today));

        var export = await exporter.ExportAsync(
            TransactionFilter.Empty,
            [ExportColumn.Kind, ExportColumn.Date]);

        Assert.StartsWith("Kind,Date", Text(export), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_filename_says_what_is_in_the_file()
    {
        var repo = new RecordingRepository();
        var exporter = new CsvExporter(repo, new StoppedClock(Today));

        var everything = await exporter.ExportAsync(TransactionFilter.Empty);
        Assert.Equal("elefi-all-2026-08-30.csv", everything.FileName);

        var filtered = await exporter.ExportAsync(
            new TransactionFilter { From = new DateOnly(2026, 4, 1), To = new DateOnly(2027, 3, 31) },
            containerNames: ["HDFC Savings"]);

        Assert.Contains("hdfc-savings", filtered.FileName, StringComparison.Ordinal);
        Assert.Contains("2026-04-01", filtered.FileName, StringComparison.Ordinal);
    }

    /// Decodes the CSV without its byte-order mark. The BOM is required (FR-7.3) so Excel
    /// renders the rupee sign, but it is not part of the text being asserted on.
    private static string Text(CsvExport export) =>
        System.Text.Encoding.UTF8.GetString(export.Contents.Span).TrimStart('﻿');

    private static Transaction Row(string counterparty, long minor, string? description = null)
    {
        var container = new Party { Kind = PartyKind.Container, ContainerId = Guid.NewGuid() };
        var external = new Party { Kind = PartyKind.External, Name = counterparty };

        return new Transaction
        {
            SourceParty = container,
            SourcePartyId = container.Id,
            DestinationParty = external,
            DestinationPartyId = external.Id,
            SourceAmountMinor = minor,
            DestinationAmountMinor = minor,
            OccurredOn = Today,
            Description = description,
        };
    }

    private sealed class StoppedClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        public DateOnly Today { get; } = today;

        public TimeOnly TimeOfDay { get; } = new(9, 0);
    }

    private sealed class RecordingRepository : ITransactionRepository
    {
        public IReadOnlyList<Transaction> Rows { get; set; } = [];

        public TransactionFilter? LastFilter { get; private set; }

        public int? LastTake { get; private set; }

        public Task<IReadOnlyList<Transaction>> QueryAsync(
            TransactionFilter filter, int skip = 0, int? take = null, CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            LastTake = take;
            return Task.FromResult(Rows);
        }

        public Task<int> CountAsync(TransactionFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.Count);

        public Task<Transaction?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Transaction?>(null);

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ContainerBalance>> BalancesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContainerBalance>>([]);

        public Task<SpendBreakdown> SpendByLabelAsync(
            TransactionFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpendBreakdown([], 0));

        public Task<FlowTotals> TotalsAsync(TransactionFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(default(FlowTotals));

        public Task<IReadOnlyList<Transaction>> ListDeletedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>([]);
    }
}
