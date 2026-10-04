using EleFi.Application.Abstractions;
using EleFi.Domain.Alerts;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads and writes Parse Rules and Capture Suggestions.
/// </summary>
/// <remarks>
/// Note what this class never does: store a message body or a sender. Suggestions carry
/// only the named captures a rule extracted plus a one-way fingerprint (SM1).
/// </remarks>
public sealed class SuggestionRepository(EleFiDbContext db) : ISuggestionRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ParseRule>> ListRulesAsync(CancellationToken cancellationToken = default) =>
        await db.ParseRules
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CaptureSuggestion>> ListPendingAsync(CancellationToken cancellationToken = default) =>
        await db.CaptureSuggestions
            .Where(s => s.State == SuggestionState.Pending)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CaptureSuggestion>> ListSinceAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        await db.CaptureSuggestions
            .Where(s => s.CreatedAt >= since)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<int> CountPendingAsync(CancellationToken cancellationToken = default) =>
        db.CaptureSuggestions.CountAsync(s => s.State == SuggestionState.Pending, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string fingerprint, CancellationToken cancellationToken = default) =>
        db.CaptureSuggestions.AnyAsync(
            s => s.Fingerprint == fingerprint || s.CorroboratingFingerprint == fingerprint,
            cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(CaptureSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        db.CaptureSuggestions.Add(suggestion);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(CaptureSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        // Attached by the query that returned it, so saving is enough. Update() covers a
        // detached instance without double-attaching a tracked one.
        if (db.Entry(suggestion).State == EntityState.Detached)
        {
            db.CaptureSuggestions.Update(suggestion);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<CaptureSuggestion?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.CaptureSuggestions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // A hard delete, and the only one in the app (SM5). Soft delete protects
        // user-entered data; this is machine output the user rejected, and keeping it would
        // mean retaining message-derived content after an explicit no.
        await db.CaptureSuggestions
            .Where(s => s.Id == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task MarkConfirmedAsync(
        Guid id,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var suggestion = await db.CaptureSuggestions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (suggestion is null)
        {
            return;
        }

        suggestion.State = SuggestionState.Confirmed;
        suggestion.TransactionId = transactionId;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> PurgeExpiredAsync(
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default) =>
        await db.CaptureSuggestions
            .Where(s => s.State == SuggestionState.Pending && s.ExpiresAt <= asOf)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
