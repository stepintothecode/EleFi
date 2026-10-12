using EleFi.Application.Abstractions;
using EleFi.Domain.Parties;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence.Repositories;

/// <summary>
/// Contacts: external parties with their details, links and aliases, and the merge that
/// folds one into another.
/// </summary>
/// <remarks>
/// A merge and its undo each run in one database transaction, so there is never a moment
/// where some of a contact's transactions have moved and some have not. Every transaction
/// moved goes through the change tracker rather than a bulk statement, so the audit triggers
/// record the move in its history and a screen holding it sees the change.
/// </remarks>
public sealed class ContactRepository(EleFiDbContext db, IClock clock) : IContactRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Counted in the database: a list of hundreds of contacts must not load the ledger.
        var rows = await db.Parties
            .AsNoTracking()
            .Where(p => p.Kind == PartyKind.External && p.Name != null)
            .Select(p => new
            {
                p.Id,
                Name = p.Name!,
                p.LastUsedAt,
                Transactions = db.Transactions.Count(t => t.SourcePartyId == p.Id || t.DestinationPartyId == p.Id),
                Details = db.ContactDetails.Count(d => d.PartyId == p.Id),
                Linked = p.PhoneContactKey != null,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new ContactSummary(r.Id, r.Name, r.Transactions, r.LastUsedAt, r.Details, r.Linked))];
    }

    /// <inheritdoc />
    public Task<Party?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Parties.FirstOrDefaultAsync(p => p.Id == id && p.Kind == PartyKind.External && p.Name != null, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> NameTakenAsync(string name, Guid? excluding = null, CancellationToken cancellationToken = default)
    {
        var normalised = Party.NormaliseName(name ?? string.Empty);

        // Live contacts and aliases alike: an alias is a name in use, because capturing with
        // it lands on its contact. Except the contact's own, so renaming back to an old name works.
        return await db.Parties
            .IgnoreQueryFilters()
            .AnyAsync(
                p => p.Kind == PartyKind.External
                     && p.Name != null
                     && EF.Functions.Collate(p.Name, "NOCASE") == normalised
                     && (p.DeletedAt == null || p.MergedIntoPartyId != null)
                     && (excluding == null || (p.Id != excluding && p.MergedIntoPartyId != excluding)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Party> AddAsync(string name, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var party = new Party
        {
            Kind = PartyKind.External,
            Name = Party.NormaliseName(name),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Parties.Add(party);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return party;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Party contact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);
        contact.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAliasAsync(Guid contactId, string name, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        // A row of its own, soft-deleted so it is in no list, pointing at the contact. The same
        // shape a merge leaves behind, so one lookup serves both.
        db.Parties.Add(new Party
        {
            Kind = PartyKind.External,
            Name = Party.NormaliseName(name),
            MergedIntoPartyId = contactId,
            CreatedAt = now,
            UpdatedAt = now,
            DeletedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> AliasesAsync(Guid contactId, CancellationToken cancellationToken = default) =>
        await db.Parties
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.MergedIntoPartyId == contactId && p.DeletedAt != null && p.Name != null)
            .OrderBy(p => p.CreatedAt)
            .Select(p => p.Name!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactDetail>> DetailsAsync(Guid contactId, CancellationToken cancellationToken = default) =>
        await db.ContactDetails
            .Where(d => d.PartyId == contactId)
            .OrderBy(d => d.Kind)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AddDetailAsync(ContactDetail detail, CancellationToken cancellationToken = default)
    {
        db.ContactDetails.Add(detail);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveDetailAsync(Guid detailId, CancellationToken cancellationToken = default)
    {
        var detail = await db.ContactDetails.FirstOrDefaultAsync(d => d.Id == detailId, cancellationToken).ConfigureAwait(false);
        if (detail is null)
        {
            return;
        }

        detail.DeletedAt = detail.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactLink>> LinksAsync(Guid? contactId, CancellationToken cancellationToken = default) =>
        await db.ContactLinks
            .Where(l => contactId == null || l.PartyId == contactId || l.OtherPartyId == contactId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AddLinkAsync(ContactLink link, CancellationToken cancellationToken = default)
    {
        db.ContactLinks.Add(link);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Not left tracked: the next save would try the same insert again.
            db.Entry(link).State = EntityState.Detached;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveLinkAsync(Guid linkId, CancellationToken cancellationToken = default)
    {
        var link = await db.ContactLinks.FirstOrDefaultAsync(l => l.Id == linkId, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            return;
        }

        link.DeletedAt = link.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ContactMerge> MergeAsync(Guid keepId, Guid absorbId, CancellationToken cancellationToken = default)
    {
        var kept = await LiveContactAsync(keepId, cancellationToken).ConfigureAwait(false);
        var absorbed = await LiveContactAsync(absorbId, cancellationToken).ConfigureAwait(false);
        if (keepId == absorbId)
        {
            throw new InvalidOperationException("A contact cannot be merged into itself.");
        }

        var now = clock.UtcNow;
        await using var scope = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Deleted transactions too, so restoring one later does not bring back a payee that is
        // now only an alias. T1 holds: a transaction has at most one external end, so moving it
        // can never make both ends the same contact.
        var transactions = await db.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.SourcePartyId == absorbId || t.DestinationPartyId == absorbId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var movedSource = new List<Guid>();
        var movedDestination = new List<Guid>();
        foreach (var transaction in transactions)
        {
            if (transaction.SourcePartyId == absorbId)
            {
                transaction.SourcePartyId = keepId;
                movedSource.Add(transaction.Id);
            }

            if (transaction.DestinationPartyId == absorbId)
            {
                transaction.DestinationPartyId = keepId;
                movedDestination.Add(transaction.Id);
            }

            transaction.UpdatedAt = now;
        }

        var details = await db.ContactDetails.Where(d => d.PartyId == absorbId).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var detail in details)
        {
            detail.PartyId = keepId;
            detail.UpdatedAt = now;
        }

        // Links move to the kept contact, except one between the two (which would join it to
        // itself) and one repeating a link it already has: those are retired, softly.
        var keptLinks = await db.ContactLinks.Where(l => l.PartyId == keepId || l.OtherPartyId == keepId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var links = await db.ContactLinks.Where(l => l.PartyId == absorbId || l.OtherPartyId == absorbId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var movedLinks = new List<Guid>();
        var retiredLinks = new List<Guid>();
        foreach (var link in links)
        {
            var far = link.Other(absorbId);
            if (far == keepId || keptLinks.Exists(k => k.Other(keepId) == far))
            {
                link.DeletedAt = link.UpdatedAt = now;
                retiredLinks.Add(link.Id);
                continue;
            }

            var moved = ContactLink.Between(keepId, far, link.Relation, now);
            (link.PartyId, link.OtherPartyId, link.UpdatedAt) = (moved.PartyId, moved.OtherPartyId, now);
            movedLinks.Add(link.Id);
        }

        // Aliases of the absorbed contact point straight at the kept one, so no lookup ever
        // has to follow a chain.
        var aliases = await db.Parties.IgnoreQueryFilters().Where(p => p.MergedIntoPartyId == absorbId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var alias in aliases)
        {
            alias.MergedIntoPartyId = keepId;
            alias.UpdatedAt = now;
        }

        var merge = new ContactMerge(
            keepId,
            absorbId,
            movedSource,
            movedDestination,
            [.. details.Select(d => d.Id)],
            movedLinks,
            retiredLinks,
            [.. aliases.Select(a => a.Id)],
            kept.Notes,
            kept.PhoneContactKey,
            absorbed.UsageCount);

        kept.Notes = string.IsNullOrWhiteSpace(absorbed.Notes) ? kept.Notes
            : string.IsNullOrWhiteSpace(kept.Notes) ? absorbed.Notes
            : $"{kept.Notes}\n\n{absorbed.Notes}";
        kept.PhoneContactKey ??= absorbed.PhoneContactKey;
        kept.UsageCount += absorbed.UsageCount;
        kept.LastUsedAt = Latest(kept.LastUsedAt, absorbed.LastUsedAt);
        kept.UpdatedAt = now;

        absorbed.MergedIntoPartyId = keepId;
        absorbed.DeletedAt = absorbed.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
        return merge;
    }

    /// <inheritdoc />
    public async Task UndoMergeAsync(ContactMerge merge, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(merge);

        var now = clock.UtcNow;
        await using var scope = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var absorbed = await db.Parties.IgnoreQueryFilters().FirstAsync(p => p.Id == merge.AbsorbedId, cancellationToken)
            .ConfigureAwait(false);
        absorbed.MergedIntoPartyId = null;
        absorbed.DeletedAt = null;
        absorbed.UpdatedAt = now;

        var kept = await db.Parties.IgnoreQueryFilters().FirstAsync(p => p.Id == merge.KeptId, cancellationToken)
            .ConfigureAwait(false);
        kept.Notes = merge.KeptNotesBefore;
        kept.PhoneContactKey = merge.KeptPhoneKeyBefore;
        kept.UsageCount = Math.Max(0, kept.UsageCount - merge.UsageAdded);
        kept.UpdatedAt = now;

        var ids = merge.MovedSourceIds.Concat(merge.MovedDestinationIds).Distinct().ToList();
        foreach (var transaction in await db.Transactions.IgnoreQueryFilters().Where(t => ids.Contains(t.Id))
                     .ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            if (merge.MovedSourceIds.Contains(transaction.Id))
            {
                transaction.SourcePartyId = merge.AbsorbedId;
            }

            if (merge.MovedDestinationIds.Contains(transaction.Id))
            {
                transaction.DestinationPartyId = merge.AbsorbedId;
            }

            transaction.UpdatedAt = now;
        }

        foreach (var detail in await db.ContactDetails.IgnoreQueryFilters().Where(d => merge.MovedDetailIds.Contains(d.Id))
                     .ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            detail.PartyId = merge.AbsorbedId;
            detail.UpdatedAt = now;
        }

        foreach (var link in await db.ContactLinks.IgnoreQueryFilters().Where(l => merge.MovedLinkIds.Contains(l.Id))
                     .ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            var back = ContactLink.Between(merge.AbsorbedId, link.Other(merge.KeptId), link.Relation, now);
            (link.PartyId, link.OtherPartyId, link.UpdatedAt) = (back.PartyId, back.OtherPartyId, now);
        }

        foreach (var link in await db.ContactLinks.IgnoreQueryFilters().Where(l => merge.RetiredLinkIds.Contains(l.Id))
                     .ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            link.DeletedAt = null;
            link.UpdatedAt = now;
        }

        foreach (var alias in await db.Parties.IgnoreQueryFilters().Where(p => merge.RepointedAliasIds.Contains(p.Id))
                     .ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            alias.MergedIntoPartyId = merge.AbsorbedId;
            alias.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Party> LiveContactAsync(Guid id, CancellationToken cancellationToken) =>
        await FindAsync(id, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException("Only two live contacts can be merged.");

    private static DateTimeOffset? Latest(DateTimeOffset? a, DateTimeOffset? b) =>
        a is null ? b : b is null ? a : a > b ? a : b;
}
