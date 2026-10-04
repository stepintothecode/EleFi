using System.Globalization;
using EleFi.Application.Abstractions;
using EleFi.Application.Backup;
using EleFi.Domain.Containers;
using EleFi.Domain.Labels;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

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
    /// <inheritdoc />
    public async Task<BackupFile> ExportAsync(CancellationToken cancellationToken = default)
    {
        var containers = await db.Containers.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var labels = await db.Labels.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var apps = await db.Apps.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

        // External parties as their own list; container parties travel with their container
        // so a restore can recreate the pair with both ids intact.
        var parties = await db.Parties.AsNoTracking()
            .Where(p => p.Kind == PartyKind.External)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var containerParties = await db.Parties.AsNoTracking()
            .Where(p => p.ContainerId != null)
            .ToDictionaryAsync(p => p.ContainerId!.Value, p => p.Id, cancellationToken)
            .ConfigureAwait(false);

        var transactions = await db.Transactions.AsNoTracking()
            .Include(t => t.Labels)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new BackupFile
        {
            ExportedAt = clock.UtcNow,
            AppVersion = "0.3.0",
            Containers = [.. containers.Select(c => new BackupContainer
            {
                Id = c.Id,
                PartyId = containerParties.GetValueOrDefault(c.Id),
                Name = c.Name,
                Kind = c.Kind.ToString(),
                CurrencyCode = c.CurrencyCode,
                OpeningBalanceMinor = c.OpeningBalanceMinor,
                OpeningBalanceAsOf = c.OpeningBalanceAsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                IsArchived = c.IsArchived,
                InstitutionName = c.InstitutionName,
                AccountNumberLast4 = c.AccountNumberLast4,
                Colour = c.Colour,
            })],
            Labels = [.. labels.Select(l => new BackupLabel
            {
                Id = l.Id,
                Name = l.Name,
                Colour = l.Colour,
                SortOrder = l.SortOrder,
            })],
            Apps = [.. apps.Select(a => new BackupApp { Id = a.Id, Name = a.Name })],
            Parties = [.. parties.Select(p => new BackupParty { Id = p.Id, Name = p.Name ?? string.Empty })],
            Transactions = [.. transactions.Select(t => new BackupTransaction
            {
                Id = t.Id,
                SourcePartyId = t.SourcePartyId,
                DestinationPartyId = t.DestinationPartyId,
                SourceAmountMinor = t.SourceAmountMinor,
                SourceCurrencyCode = t.SourceCurrencyCode,
                DestinationAmountMinor = t.DestinationAmountMinor,
                DestinationCurrencyCode = t.DestinationCurrencyCode,
                OccurredOn = t.OccurredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                OccurredAtTime = t.OccurredAtTime?.ToString("HH\\:mm", CultureInfo.InvariantCulture),
                Description = t.Description,
                LabelIds = [.. t.Labels.Select(l => l.LabelId)],
                MarketplaceAppId = t.MarketplaceAppId,
                PaymentAppId = t.PaymentAppId,
                NeedsReview = t.NeedsReview,
                CaptureSource = t.CaptureSource.ToString(),
            })],
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

        // Everything goes first, including the seeded labels, or the restore would merge
        // into whatever is already here and produce a third state matching neither.
        await wipe.WipeEverythingAsync(cancellationToken).ConfigureAwait(false);

        await db.TransactionLabels.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Labels.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;

        foreach (var label in file.Labels)
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

        foreach (var container in file.Containers)
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

        foreach (var app in file.Apps)
        {
            db.Apps.Add(new Domain.Apps.App { Id = app.Id, Name = app.Name, CreatedAt = now, UpdatedAt = now });
        }

        foreach (var party in file.Parties)
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
        foreach (var t in file.Transactions)
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
        return file.Transactions.Count;
    }

}
