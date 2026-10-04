using EleFi.Application.Abstractions;
using EleFi.Domain.Apps;
using EleFi.Domain.Containers;
using EleFi.Domain.Labels;
using EleFi.Domain.Parties;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes Money Containers, and the Party row each one owns.</summary>
public sealed class ContainerRepository(EleFiDbContext db, IClock clock) : IContainerRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Container>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Containers
            .OrderBy(c => c.IsArchived)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Container>> ListSelectableAsync(CancellationToken cancellationToken = default) =>
        await db.Containers
            .Where(c => !c.IsArchived)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<Container?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Containers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Container>> FindByLast4Async(
        string last4,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(last4))
        {
            return [];
        }

        return await db.Containers
            .Where(c => !c.IsArchived && c.AccountNumberLast4 == last4)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(Container container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);

        db.Containers.Add(container);

        // P3: exactly one Party row exists per Container, created with it. Doing this here
        // rather than leaving it to callers is what stops a container that cannot be used
        // as either end of a transaction.
        db.Parties.Add(new Party
        {
            Kind = PartyKind.Container,
            ContainerId = container.Id,
            Name = null,
            CreatedAt = container.CreatedAt,
            UpdatedAt = container.UpdatedAt,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Container container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        container.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Party?> PartyForAsync(Guid containerId, CancellationToken cancellationToken = default) =>
        db.Parties.FirstOrDefaultAsync(p => p.ContainerId == containerId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> NameTakenAsync(
        string name,
        Guid? excluding = null,
        CancellationToken cancellationToken = default)
    {
        var normalised = (name ?? string.Empty).Trim();

        return db.Containers.AnyAsync(
            c => EF.Functions.Collate(c.Name, "NOCASE") == normalised
                 && (excluding == null || c.Id != excluding),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> TransactionCountAsync(Guid containerId, CancellationToken cancellationToken = default)
    {
        var party = await db.Parties
            .Where(p => p.ContainerId == containerId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (party is null)
        {
            return 0;
        }

        // Either end: a container is referenced whether money arrived or left.
        return await db.Transactions
            .CountAsync(t => t.SourcePartyId == party || t.DestinationPartyId == party, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SoftDeleteAsync(Guid containerId, CancellationToken cancellationToken = default)
    {
        var container = await db.Containers
            .FirstOrDefaultAsync(c => c.Id == containerId, cancellationToken)
            .ConfigureAwait(false);

        if (container is null)
        {
            return;
        }

        var now = clock.UtcNow;
        container.DeletedAt = now;
        container.UpdatedAt = now;

        // The Party goes with it (P3). Leaving it behind would keep the container
        // selectable as a transaction end after it had gone.
        var party = await db.Parties
            .FirstOrDefaultAsync(p => p.ContainerId == containerId, cancellationToken)
            .ConfigureAwait(false);

        if (party is not null)
        {
            party.DeletedAt = now;
            party.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Reads and writes parties, the one pool feeding both ends of a transaction.</summary>
public sealed class PartyRepository(EleFiDbContext db, IClock clock) : IPartyRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Party>> SuggestAsync(
        string? search,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        // External only. A container's Party row has no name, and offering it here put blank
        // entries into every "Paid to" list.
        var query = db.Parties.Where(p => p.Kind == PartyKind.External && p.Name != null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => EF.Functions.Like(p.Name!, $"%{term}%"));
        }

        // Recency before frequency: what you paid yesterday is a better guess than what you
        // paid most often two years ago.
        return await query
            .OrderByDescending(p => p.LastUsedAt)
            .ThenByDescending(p => p.UsageCount)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Party>> ListExternalAsync(CancellationToken cancellationToken = default) =>
        await db.Parties
            .AsNoTracking()
            .Where(p => p.Kind == PartyKind.External && p.Name != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<Party?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Parties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<Party> GetOrCreateExternalAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalised = Party.NormaliseName(name);

        // P4: "Rahul", "rahul " and "RAHUL" are one party, so the suggestion list does not
        // fragment into three entries for one person. NOCASE is SQLite's own
        // case-insensitive collation, so this compares in the database rather than pulling
        // every party into memory to compare here.
        var existing = await db.Parties
            .FirstOrDefaultAsync(
                p => p.Kind == PartyKind.External
                     && p.Name != null
                     && EF.Functions.Collate(p.Name, "NOCASE") == normalised,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var now = clock.UtcNow;
        var party = new Party
        {
            Kind = PartyKind.External,
            Name = normalised,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Parties.Add(party);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return party;
    }

    /// <inheritdoc />
    public async Task TouchAsync(IEnumerable<Guid> partyIds, CancellationToken cancellationToken = default)
    {
        var ids = partyIds.Distinct().ToList();
        var parties = await db.Parties.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var party in parties)
        {
            party.UsageCount++;
            party.LastUsedAt = clock.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Reads and writes labels.</summary>
public sealed class LabelRepository(EleFiDbContext db, IClock clock) : ILabelRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Label>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Labels
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AddAsync(Label label, CancellationToken cancellationToken = default)
    {
        db.Labels.Add(label);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Label?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Labels.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task UpdateAsync(Label label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(label);
        label.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var label = await db.Labels.FirstOrDefaultAsync(l => l.Id == id, cancellationToken).ConfigureAwait(false);
        if (label is null)
        {
            return;
        }

        // Detach it from every transaction. No replacement is needed: a transaction that
        // loses its only label becomes unlabelled, which is now a normal state, and the
        // amount is untouched either way.
        await db.TransactionLabels
            .Where(tl => tl.LabelId == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = clock.UtcNow;
        label.DeletedAt = now;
        label.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> NameTakenAsync(
        string name,
        Guid? excluding = null,
        CancellationToken cancellationToken = default)
    {
        var normalised = (name ?? string.Empty).Trim();

        // Globally unique now that labels are flat. Two called Food would simply be
        // indistinguishable in the picker and in every filter chip.
        return db.Labels.AnyAsync(
            l => EF.Functions.Collate(l.Name, "NOCASE") == normalised
                 && (excluding == null || l.Id != excluding),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> TransactionCountAsync(Guid labelId, CancellationToken cancellationToken = default) =>
        db.TransactionLabels.CountAsync(tl => tl.LabelId == labelId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> UsageCountsAsync(CancellationToken cancellationToken = default)
    {
        // The join table has no soft-delete filter of its own, so the Any() is what keeps a
        // deleted transaction's labels out. Transactions' query filter applies inside it.
        var counts = await db.TransactionLabels
            .Where(tl => db.Transactions.Any(t => t.Id == tl.TransactionId))
            .GroupBy(tl => tl.LabelId)
            .Select(g => new { LabelId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return counts.ToDictionary(x => x.LabelId, x => x.Count);
    }
}

/// <summary>Reads and writes Apps: marketplaces and payment rails, one pool.</summary>
public sealed class AppRepository(EleFiDbContext db, IClock clock) : IAppRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<App>> SuggestAsync(
        string? search,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = db.Apps.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => EF.Functions.Like(a.Name, $"%{term}%"));
        }

        return await query
            .OrderByDescending(a => a.LastUsedAt)
            .ThenByDescending(a => a.UsageCount)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<App>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Apps.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<App> GetOrCreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalised = App.Normalise(name);

        // A1: one pool, case-insensitive, so "zomato" and "Zomato" do not both appear.
        var existing = await db.Apps
            .FirstOrDefaultAsync(a => EF.Functions.Collate(a.Name, "NOCASE") == normalised, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var now = clock.UtcNow;
        var app = new App { Name = normalised, CreatedAt = now, UpdatedAt = now };

        db.Apps.Add(app);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return app;
    }

    /// <inheritdoc />
    public async Task TouchAsync(IEnumerable<Guid> appIds, CancellationToken cancellationToken = default)
    {
        var ids = appIds.Distinct().ToList();
        var apps = await db.Apps.Where(a => ids.Contains(a.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var app in apps)
        {
            app.UsageCount++;
            app.LastUsedAt = clock.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

