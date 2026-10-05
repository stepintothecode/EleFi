using System.Globalization;
using EleFi.Application.Abstractions;
using EleFi.Application.Backup;
using EleFi.Domain.Containers;
using EleFi.Domain.Labels;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EleFi.Infrastructure.Persistence;

/// <summary>
/// Exports the database to JSON and restores it back.
/// </summary>
/// <remarks>
/// <para>
/// Enums are written by name, not number. A backup outlives the build that wrote it, and
/// reordering an enum later would silently reinterpret every row: a Wallet becoming a
/// FixedDeposit changes which tile its money lands in with nothing to notice.
/// </para>
/// <para>
/// Identifiers are preserved, so a restored transaction still points at the same container
/// and the same labels. Regenerating them would work too, but it makes two backups of the
/// same data incomparable.
/// </para>
/// </remarks>
public sealed class LocalBackup(EleFiDbContext db, DataWipe wipe, IClock clock) : ILocalBackup
{
    private const string AppVersion = "0.3.0";

    private const string NewerVersion =
        "That backup was written by a newer version of EleFi. Update the app before restoring it, rather than importing part of it.";

    /// <inheritdoc />
    public async Task<BackupFile> ExportAsync(CancellationToken cancellationToken = default)
    {
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        var tables = new Dictionary<string, List<JsonObject>>(StringComparer.Ordinal);

        await db.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var table in BackupTables.Of(db.Model))
            {
                using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText =
                    $"SELECT {string.Join(", ", table.Columns.Select(c => Quote(c.Name)))} FROM {Quote(table.Name)} ORDER BY rowid";

                var rows = new List<JsonObject>();
                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var row = new JsonObject();
                    for (var i = 0; i < table.Columns.Count; i++)
                    {
                        row[table.Columns[i].Name] = table.Columns[i].ToJson(reader.IsDBNull(i) ? null : reader.GetValue(i));
                    }

                    rows.Add(row);
                }

                tables[table.Name] = rows;
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        return new BackupFile
        {
            ExportedAt = clock.UtcNow,
            AppVersion = AppVersion,
            DatabaseVersion = applied.LastOrDefault(),
            Tables = tables,
        };
    }

    /// <inheritdoc />
    public async Task<int> RestoreAsync(BackupFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.SchemaVersion > BackupFile.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"That backup was written by a newer version of EleFi (format {file.SchemaVersion}). "
                + "Update the app before restoring it, rather than importing part of it.");
        }

        return file.Tables is null
            ? await RestoreFormatOneAsync(file, cancellationToken).ConfigureAwait(false)
            : await RestoreTablesAsync(file, file.Tables, cancellationToken).ConfigureAwait(false);
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>
    /// Replaces every table with the file's rows, in one transaction.
    /// </summary>
    /// <remarks>
    /// Checked in full before anything is touched: a table or column this version does not
    /// know means the file came from a newer one, and importing the rest would quietly drop
    /// whatever that was. Foreign keys are checked at commit, so the order rows go in does not
    /// matter, and the audit triggers are off while they do, so the restored history is not
    /// joined by a "created" event for every row. Any failure rolls the whole thing back and
    /// leaves the device exactly as it was.
    /// </remarks>
    private async Task<int> RestoreTablesAsync(BackupFile file, Dictionary<string, List<JsonObject>> tables, CancellationToken cancellationToken)
    {
        var known = BackupTables.Of(db.Model).ToDictionary(t => t.Name, StringComparer.Ordinal);
        var migrations = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);

        if (file.DatabaseVersion is { } version && !migrations.Contains(version))
        {
            throw new InvalidOperationException(NewerVersion);
        }

        foreach (var (name, rows) in tables)
        {
            if (!known.TryGetValue(name, out var table)
                || rows.SelectMany(r => r.Select(p => p.Key)).Any(column => table.Column(column) is null))
            {
                throw new InvalidOperationException(NewerVersion);
            }
        }

        await using (var scope = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA defer_foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
            foreach (var statement in AuditTriggers.DropStatements)
            {
                await db.Database.ExecuteSqlRawAsync(statement, cancellationToken).ConfigureAwait(false);
            }

            foreach (var table in known.Values)
            {
                using var delete = db.Database.GetDbConnection().CreateCommand();
                delete.Transaction = scope.GetDbTransaction();
                delete.CommandText = $"DELETE FROM {Quote(table.Name)}";
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var (name, rows) in tables)
            {
                var table = known[name];
                foreach (var row in rows)
                {
                    using var insert = db.Database.GetDbConnection().CreateCommand();
                    insert.Transaction = scope.GetDbTransaction();
                    var columns = row.Select(p => table.Column(p.Key)!).ToList();
                    insert.CommandText =
                        $"INSERT INTO {Quote(name)} ({string.Join(", ", columns.Select(c => Quote(c.Name)))}) "
                        + $"VALUES ({string.Join(", ", columns.Select((_, i) => $"$p{i}"))})";

                    for (var i = 0; i < columns.Count; i++)
                    {
                        var parameter = insert.CreateParameter();
                        parameter.ParameterName = $"$p{i}";
                        parameter.Value = columns[i].FromJson(row[columns[i].Name]) ?? DBNull.Value;
                        insert.Parameters.Add(parameter);
                    }

                    await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var statement in AuditTriggers.CreateStatements)
            {
                await db.Database.ExecuteSqlRawAsync(statement, cancellationToken).ConfigureAwait(false);
            }

            await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        db.ChangeTracker.Clear();
        await new DatabaseInitialiser(db, clock).AfterRestoreAsync(cancellationToken).ConfigureAwait(false);

        return file.LiveCount("Transactions");
    }

    /// <summary>Restores a format 1 file, which listed a few fields per entity.</summary>
    private async Task<int> RestoreFormatOneAsync(BackupFile file, CancellationToken cancellationToken)
    {
        // Everything goes first, including the seeded labels, or the restore would merge
        // into whatever is already here and produce a third state matching neither.
        await wipe.WipeEverythingAsync(cancellationToken).ConfigureAwait(false);

        await db.TransactionLabels.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Labels.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;

        foreach (var label in file.Labels ?? [])
        {
            db.Labels.Add(new Label
            {
                Id = label.Id,
                Name = label.Name,
                Colour = label.Colour,
                SortOrder = label.SortOrder,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        foreach (var container in file.Containers ?? [])
        {
            // By name: a reordered enum would otherwise turn a Wallet into a FixedDeposit
            // and move its money to a different tile with nothing to notice.
            if (!Enum.TryParse<ContainerKind>(container.Kind, out var kind))
            {
                throw new InvalidOperationException(
                    $"'{container.Name}' has a kind this version does not know: {container.Kind}.");
            }

            db.Containers.Add(new Container
            {
                Id = container.Id,
                Name = container.Name,
                Kind = kind,
                CurrencyCode = container.CurrencyCode,
                OpeningBalanceMinor = container.OpeningBalanceMinor,
                OpeningBalanceAsOf = DateOnly.ParseExact(
                    container.OpeningBalanceAsOf, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                IsArchived = container.IsArchived,
                InstitutionName = container.InstitutionName,
                AccountNumberLast4 = container.AccountNumberLast4,
                Colour = container.Colour,
                CreatedAt = now,
                UpdatedAt = now,
            });

            // P3: one party per container, recreated with its original id so every
            // transaction that references it still resolves.
            db.Parties.Add(new Party
            {
                Id = container.PartyId == Guid.Empty ? Guid.CreateVersion7() : container.PartyId,
                Kind = PartyKind.Container,
                ContainerId = container.Id,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        foreach (var app in file.Apps ?? [])
        {
            db.Apps.Add(new Domain.Apps.App { Id = app.Id, Name = app.Name, CreatedAt = now, UpdatedAt = now });
        }

        foreach (var party in file.Parties ?? [])
        {
            db.Parties.Add(new Party
            {
                Id = party.Id,
                Kind = PartyKind.External,
                Name = party.Name,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Transactions last: their foreign keys need everything above to exist. Party ids
        // are preserved on both sides, so no remapping is needed.
        foreach (var t in file.Transactions ?? [])
        {
            var transaction = new Transaction
            {
                Id = t.Id,
                SourcePartyId = t.SourcePartyId,
                DestinationPartyId = t.DestinationPartyId,
                SourceAmountMinor = t.SourceAmountMinor,
                SourceCurrencyCode = t.SourceCurrencyCode,
                DestinationAmountMinor = t.DestinationAmountMinor,
                DestinationCurrencyCode = t.DestinationCurrencyCode,
                OccurredOn = DateOnly.ParseExact(t.OccurredOn, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                OccurredAtTime = string.IsNullOrEmpty(t.OccurredAtTime)
                    ? null
                    : TimeOnly.ParseExact(t.OccurredAtTime, "HH\\:mm", CultureInfo.InvariantCulture),
                Description = t.Description,
                MarketplaceAppId = t.MarketplaceAppId,
                PaymentAppId = t.PaymentAppId,
                NeedsReview = t.NeedsReview,
                CaptureSource = Enum.TryParse<CaptureSource>(t.CaptureSource, out var source)
                    ? source
                    : CaptureSource.Import,
                CreatedAt = now,
                UpdatedAt = now,
            };

            foreach (var labelId in t.LabelIds.Distinct())
            {
                transaction.Labels.Add(new TransactionLabel { TransactionId = t.Id, LabelId = labelId });
            }

            db.Transactions.Add(transaction);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return file.Transactions?.Count ?? 0;
    }

}
