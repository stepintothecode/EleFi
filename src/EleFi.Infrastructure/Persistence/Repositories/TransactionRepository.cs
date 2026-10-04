using EleFi.Application.Abstractions;
using EleFi.Domain.Balances;
using EleFi.Domain.Filters;
using EleFi.Domain.Money;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads and writes transactions. The only place their SQL is composed.
/// </summary>
/// <remarks>
/// <see cref="QueryAsync"/> is the single query behind both the transaction list and the
/// CSV export. That is not a convenience: it is what makes X1 true by construction. If the
/// export had its own query, the two would eventually disagree, and the user would find out
/// by handing an accountant a file that does not match the screen.
/// </remarks>
public sealed class TransactionRepository(EleFiDbContext db, IClock clock) : ITransactionRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Transaction>> QueryAsync(
        TransactionFilter filter,
        int skip = 0,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        var query = Filtered(filter);

        // The list order. The export inherits it, so "same rows, same order" needs no
        // second thought anywhere else.
        query = query
            .OrderByDescending(t => t.OccurredOn)
            .ThenByDescending(t => t.CreatedAt)
            .Skip(skip);

        if (take is { } n)
        {
            query = query.Take(n);
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(TransactionFilter filter, CancellationToken cancellationToken = default) =>
        Filtered(filter).CountAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Transaction?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Included().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Re-read so the caller gets the parties and label loaded, which the derived Kind
        // needs. Cheaper than making every caller remember.
        await db.Entry(transaction).Reference(t => t.SourceParty).LoadAsync(cancellationToken).ConfigureAwait(false);
        await db.Entry(transaction).Reference(t => t.DestinationParty).LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        transaction.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (transaction is null)
        {
            return;
        }

        // Setting DeletedAt is what fires the delete trigger and what removes the row from
        // every balance, report, and export at once (T9). Nothing is destroyed.
        transaction.DeletedAt = clock.UtcNow;
        transaction.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters, because by definition the row is filtered out right now.
        var transaction = await db.Transactions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (transaction is null)
        {
            return;
        }

        transaction.DeletedAt = null;
        transaction.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContainerBalance>> BalancesAsync(CancellationToken cancellationToken = default)
    {
        var containers = await db.Containers.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var parties = await db.Parties.AsNoTracking()
            .Where(p => p.ContainerId != null)
            .ToDictionaryAsync(p => p.ContainerId!.Value, p => p.Id, cancellationToken)
            .ConfigureAwait(false);

        // Two grouped sums rather than loading every transaction. At 50,000 rows SQLite
        // does this in single-digit milliseconds given the party indexes.
        var inflow = await db.Transactions.AsNoTracking()
            .GroupBy(t => t.DestinationPartyId)
            .Select(g => new { PartyId = g.Key, Total = g.Sum(t => t.DestinationAmountMinor) })
            .ToDictionaryAsync(x => x.PartyId, x => x.Total, cancellationToken)
            .ConfigureAwait(false);

        var outflow = await db.Transactions.AsNoTracking()
            .GroupBy(t => t.SourcePartyId)
            .Select(g => new { PartyId = g.Key, Total = g.Sum(t => t.SourceAmountMinor) })
            .ToDictionaryAsync(x => x.PartyId, x => x.Total, cancellationToken)
            .ConfigureAwait(false);

        var balances = new List<ContainerBalance>(containers.Count);

        foreach (var container in containers)
        {
            var minor = container.OpeningBalanceMinor;

            if (parties.TryGetValue(container.Id, out var partyId))
            {
                minor += inflow.GetValueOrDefault(partyId);
                minor -= outflow.GetValueOrDefault(partyId);
            }

            balances.Add(new ContainerBalance(
                container.Id,
                container.Name,
                container.Kind,
                Money.SignedMinor(minor, container.Currency)));
        }

        return balances;
    }

    /// <inheritdoc />
    public async Task<SpendBreakdown> SpendByLabelAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        // D1: Self Transfers are excluded from every spend aggregate. A Debit is
        // Internal -> External, so requiring an internal source and an external destination
        // is the exclusion, expressed once.
        var rows = await db.Transactions.AsNoTracking()
            .Include(t => t.Labels).ThenInclude(tl => tl.Label)
            .Include(t => t.SourceParty)
            .Include(t => t.DestinationParty)
            .Where(t => t.OccurredOn >= fromDate && t.OccurredOn <= toDate)
            .Where(t => t.SourceParty!.Kind == PartyKind.Container
                     && t.DestinationParty!.Kind == PartyKind.External)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Counted once, before the labels fan the rows out. This is the only figure that
        // equals the money that actually moved.
        var total = rows.Sum(t => t.SourceAmountMinor);

        // One entry per (transaction, label) pair, so a transaction with two labels
        // contributes its full amount to both. That overlap is the deliberate consequence of
        // ADR-0013 and the reason the total above is computed separately.
        var byLabel = rows
            .SelectMany(t => t.Labels
                .Where(l => l.Label is not null)
                .Select(l => new { l.Label!.Id, l.Label.Name, l.Label.Colour, t.SourceAmountMinor }))
            .GroupBy(x => new { x.Id, x.Name, x.Colour })
            .Select(g => new LabelSpend(
                g.Key.Id,
                g.Key.Name,
                g.Key.Colour,
                g.Sum(x => x.SourceAmountMinor)))
            .ToList();

        // Unlabelled is a real and common state now that labels are optional, so it gets a
        // bucket rather than vanishing from the breakdown entirely.
        var unlabelled = rows.Where(t => t.Labels.Count == 0).Sum(t => t.SourceAmountMinor);
        if (unlabelled > 0)
        {
            byLabel.Add(new LabelSpend(null, "Unlabelled", null, unlabelled));
        }

        return new SpendBreakdown(
            [.. byLabel.OrderByDescending(s => s.AmountMinor)],
            total);
    }

    private IQueryable<Transaction> Included() =>
        db.Transactions
            .Include(t => t.SourceParty)
            .Include(t => t.DestinationParty)
            .Include(t => t.Labels).ThenInclude(tl => tl.Label)
            .Include(t => t.MarketplaceApp)
            .Include(t => t.PaymentApp);

    /// <summary>
    /// Translates a filter into a query. The one place filter semantics live.
    /// </summary>
    /// <remarks>
    /// Dimensions compose with AND; multi-select within a dimension is OR. The soft-delete
    /// exclusion is not here because it is a global query filter on the context, applied
    /// once for every query rather than remembered per method.
    /// </remarks>
    private IQueryable<Transaction> Filtered(TransactionFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var query = Included().AsNoTracking();

        if (filter.From is { } from)
        {
            query = query.Where(t => t.OccurredOn >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(t => t.OccurredOn <= to);
        }

        if (filter.ContainerIds.Count > 0)
        {
            var ids = filter.ContainerIds.ToList();

            // Either end. This is what makes a container filter an account statement
            // rather than a list of spending: money that arrived belongs in the statement
            // just as much as money that left.
            query = query.Where(t =>
                (t.SourceParty!.ContainerId != null && ids.Contains(t.SourceParty.ContainerId!.Value))
                || (t.DestinationParty!.ContainerId != null && ids.Contains(t.DestinationParty.ContainerId!.Value)));
        }

        if (filter.Kinds.Count > 0)
        {
            // Kind is derived, never stored, so it is filtered by the party shape that
            // defines it rather than by a column that could disagree with the parties.
            var wantsDebit = filter.Kinds.Contains(TransactionKind.Debit);
            var wantsCredit = filter.Kinds.Contains(TransactionKind.Credit);
            var wantsTransfer = filter.Kinds.Contains(TransactionKind.SelfTransfer);

            query = query.Where(t =>
                (wantsDebit && t.SourceParty!.Kind == PartyKind.Container && t.DestinationParty!.Kind == PartyKind.External)
                || (wantsCredit && t.SourceParty!.Kind == PartyKind.External && t.DestinationParty!.Kind == PartyKind.Container)
                || (wantsTransfer && t.SourceParty!.Kind == PartyKind.Container && t.DestinationParty!.Kind == PartyKind.Container));
        }

        if (filter.LabelIds.Count > 0)
        {
            // Any of them, not all. The question people ask is "show me anything to do with
            // Food", not "show me things that are simultaneously Food and Household".
            var ids = filter.LabelIds.ToList();
            query = query.Where(t => t.Labels.Any(tl => ids.Contains(tl.LabelId)));
        }

        if (filter.UnlabelledOnly)
        {
            query = query.Where(t => t.Labels.Count == 0);
        }

        if (filter.PartyIds.Count > 0)
        {
            var ids = filter.PartyIds.ToList();
            query = query.Where(t => ids.Contains(t.SourcePartyId) || ids.Contains(t.DestinationPartyId));
        }

        if (filter.AppIds.Count > 0)
        {
            var ids = filter.AppIds.ToList();
            query = query.Where(t =>
                (t.MarketplaceAppId != null && ids.Contains(t.MarketplaceAppId.Value))
                || (t.PaymentAppId != null && ids.Contains(t.PaymentAppId.Value)));
        }

        if (filter.MinAmountMinor is { } min)
        {
            query = query.Where(t => t.SourceAmountMinor >= min);
        }

        if (filter.MaxAmountMinor is { } max)
        {
            query = query.Where(t => t.SourceAmountMinor <= max);
        }

        if (filter.CurrencyCode is { } currency)
        {
            query = query.Where(t => t.SourceCurrencyCode == currency || t.DestinationCurrencyCode == currency);
        }

        if (filter.NeedsReview is { } needsReview)
        {
            query = query.Where(t => t.NeedsReview == needsReview);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(t =>
                (t.Description != null && EF.Functions.Like(t.Description, $"%{term}%"))
                || (t.SourceParty!.Name != null && EF.Functions.Like(t.SourceParty.Name, $"%{term}%"))
                || (t.DestinationParty!.Name != null && EF.Functions.Like(t.DestinationParty.Name, $"%{term}%"))
                || t.Labels.Any(tl => tl.Label != null && EF.Functions.Like(tl.Label.Name, $"%{term}%"))
                || (t.MarketplaceApp != null && EF.Functions.Like(t.MarketplaceApp.Name, $"%{term}%"))
                || (t.PaymentApp != null && EF.Functions.Like(t.PaymentApp.Name, $"%{term}%")));
        }

        return query;
    }
}
