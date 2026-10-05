using EleFi.Application.Abstractions;
using EleFi.Domain.Goals;
using EleFi.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence.Repositories;

/// <summary>Goals, their linked containers, and the transactions attributed to them.</summary>
/// <param name="db">The database.</param>
/// <param name="clock">For timestamps.</param>
public sealed class GoalRepository(EleFiDbContext db, IClock clock) : IGoalRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Goal>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Goals
            .Include(g => g.Containers)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<Goal?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Goals.Include(g => g.Containers).FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        db.Goals.Add(goal);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Goal goal, IReadOnlyCollection<Guid> containerIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(containerIds);

        goal.Containers.RemoveAll(c => !containerIds.Contains(c.ContainerId));
        foreach (var id in containerIds.Where(id => goal.Containers.All(c => c.ContainerId != id)))
        {
            goal.Containers.Add(new GoalContainer { GoalId = goal.Id, ContainerId = id });
        }

        goal.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == id, cancellationToken).ConfigureAwait(false);
        if (goal is null)
        {
            return;
        }

        // One transaction: a goal gone but still named on its transactions would count them
        // toward nothing, and a goal kept with its transactions cleared would lose its history.
        await using var scope = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;
        goal.DeletedAt = now;
        goal.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // GL6, including soft-deleted transactions, so restoring one later does not bring
        // back a pointer to a goal that is gone.
        await db.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.GoalId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.GoalId, (Guid?)null), cancellationToken)
            .ConfigureAwait(false);

        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Transaction>> ContributionsAsync(CancellationToken cancellationToken = default) =>
        await db.Transactions
            .AsNoTracking()
            .Include(t => t.SourceParty)
            .Include(t => t.DestinationParty)
            .Where(t => t.GoalId != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SetGoalAsync(Guid transactionId, Guid? goalId, CancellationToken cancellationToken = default)
    {
        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken).ConfigureAwait(false);
        if (transaction is null || transaction.GoalId == goalId)
        {
            return;
        }

        transaction.GoalId = goalId;
        transaction.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
