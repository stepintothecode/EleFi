using EleFi.Application.Abstractions;
using EleFi.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes plans.</summary>
public sealed class PlanRepository(EleFiDbContext db, IClock clock) : IPlanRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Plan>> ListOpenAsync(CancellationToken cancellationToken = default)
    {
        var open = await db.Plans
            .Where(p => p.CompletedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Ordered here rather than in SQL: DueOn is stored as ISO text, so it sorts either
        // way, but the time column is nullable and "no time" belongs after a timed one.
        return open
            .OrderBy(p => p.DueOn)
            .ThenBy(p => p.DueTime ?? TimeOnly.MaxValue)
            .ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Plan>> ListCompletedAsync(int take = 100, CancellationToken cancellationToken = default) =>
        await db.Plans
            .Where(p => p.CompletedAt != null)
            .OrderByDescending(p => p.CompletedAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<Plan?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Plans.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        db.Plans.Add(plan);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return;
        }

        plan.DeletedAt = clock.UtcNow;
        plan.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> ClaimedTransactionIdsAsync(CancellationToken cancellationToken = default)
    {
        var ids = await db.Plans
            .Where(p => p.TransactionId != null)
            .Select(p => p.TransactionId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids.ToHashSet();
    }
}
