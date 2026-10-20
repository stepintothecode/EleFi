using EleFi.Application.Abstractions;
using EleFi.Application.Typeahead;
using EleFi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence.Repositories;

/// <summary>
/// Notes and apps as they were used on transactions, for the suggestions on the forms.
/// </summary>
/// <remarks>
/// Live transactions only: a deleted one is not something the user does. A contact matches at
/// either end, because "Mom" is who was paid on a gift and who paid on a refund, and what was
/// used with her is likely again whichever way the money went.
/// </remarks>
/// <param name="db">The database.</param>
public sealed class SuggestionHistoryRepository(EleFiDbContext db) : ISuggestionHistory
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TypeaheadCandidate>> NotesAsync(string? partyName, CancellationToken cancellationToken = default)
    {
        var used = await WithParty(partyName)
            .Where(t => t.Description != null && t.Description != string.Empty)
            .Select(t => new { Text = t.Description!, t.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Count(used.Select(u => (u.Text, u.CreatedAt)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TypeaheadCandidate>> AppsAsync(AppSlot slot, string? partyName, CancellationToken cancellationToken = default)
    {
        var rows = WithParty(partyName);
        var used = slot == AppSlot.BoughtThrough
            ? await rows.Where(t => t.MarketplaceApp != null)
                .Select(t => new { Text = t.MarketplaceApp!.Name, t.CreatedAt })
                .ToListAsync(cancellationToken).ConfigureAwait(false)
            : await rows.Where(t => t.PaymentApp != null)
                .Select(t => new { Text = t.PaymentApp!.Name, t.CreatedAt })
                .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Count(used.Select(u => (u.Text, u.CreatedAt)));
    }

    private IQueryable<Domain.Transactions.Transaction> WithParty(string? partyName)
    {
        var rows = db.Transactions.AsNoTracking();
        var name = partyName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return rows;
        }

        return rows.Where(t => (t.DestinationParty!.Name != null && EF.Functions.Collate(t.DestinationParty.Name, "NOCASE") == name)
            || (t.SourceParty!.Name != null && EF.Functions.Collate(t.SourceParty.Name, "NOCASE") == name));
    }

    // One entry per text regardless of case, shown as most recently written.
    private static List<TypeaheadCandidate> Count(IEnumerable<(string Text, DateTimeOffset CreatedAt)> used) => used
        .Where(u => !string.IsNullOrWhiteSpace(u.Text))
        .GroupBy(u => u.Text.Trim(), StringComparer.OrdinalIgnoreCase)
        .Select(g => new TypeaheadCandidate(
            g.OrderByDescending(u => u.CreatedAt).First().Text.Trim(),
            g.Count(),
            g.Max(u => u.CreatedAt)))
        .ToList();
}
