using EleFi.Application.Abstractions;
using EleFi.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads the audit trail.
/// </summary>
/// <remarks>
/// Read-only by design. AU1 makes the trail append-only and AU2 puts the appending in
/// database triggers, so a method here that could insert an event would be a way to forge
/// one. A trail that can be forged is not worth reading.
/// </remarks>
public sealed class AuditRepository(EleFiDbContext db) : IAuditRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditEvent>> TimelineAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default) =>
        await db.AuditEvents
            .AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
