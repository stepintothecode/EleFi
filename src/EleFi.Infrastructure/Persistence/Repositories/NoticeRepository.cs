using EleFi.Application.Abstractions;
using EleFi.Domain.Notices;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the in-app notification list.</summary>
public sealed class NoticeRepository(EleFiDbContext db, IClock clock) : INoticeRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Notice>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Notices
            .OrderByDescending(n => n.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<int> UnreadCountAsync(CancellationToken cancellationToken = default) =>
        db.Notices.CountAsync(n => n.ReadAt == null, cancellationToken);

    /// <inheritdoc />
    public async Task UpsertAsync(Notice notice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notice);

        var existing = notice.Route is null
            ? null
            : await db.Notices.FirstOrDefaultAsync(n => n.Route == notice.Route, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            db.Notices.Add(notice);
        }
        else
        {
            existing.Title = notice.Title;
            existing.Body = notice.Body;
            existing.ReadAt = null;
            existing.UpdatedAt = clock.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetReadAsync(Guid id, bool read, CancellationToken cancellationToken = default)
    {
        var notice = await db.Notices.FirstOrDefaultAsync(n => n.Id == id, cancellationToken).ConfigureAwait(false);
        if (notice is null)
        {
            return;
        }

        notice.ReadAt = read ? clock.UtcNow : null;
        notice.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetAllReadAsync(bool read, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        DateTimeOffset? readAt = read ? now : null;

        // One statement for the whole list rather than loading every row.
        await db.Notices
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(n => n.ReadAt, readAt)
                    .SetProperty(n => n.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
