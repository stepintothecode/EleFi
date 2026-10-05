using EleFi.Application.Abstractions;
using EleFi.Domain.Alerts;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes Parse Rules, the shipped ones and the ones the user taught.</summary>
public sealed class ParseRuleRepository(EleFiDbContext db, IClock clock) : IParseRuleRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ParseRule>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.ParseRules
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<ParseRule?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.ParseRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(ParseRule rule, CancellationToken cancellationToken = default)
    {
        db.ParseRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ParseRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        rule.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await db.ParseRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return;
        }

        var now = clock.UtcNow;
        rule.DeletedAt = now;
        rule.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
