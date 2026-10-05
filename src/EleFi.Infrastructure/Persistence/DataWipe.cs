using EleFi.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence;

/// <summary>
/// Empties the database and puts back what the app cannot run without.
/// </summary>
/// <remarks>
/// <para>
/// The only hard delete of user-entered data in the app. Everything else is a soft delete
/// precisely so nothing goes permanently by accident; this is the one deliberate exception,
/// which is why the UI makes the user type the word out first.
/// </para>
/// <para>
/// The audit trail goes too. Keeping a record of what was deleted, after the user asked for
/// everything to be deleted, would mean the wipe did not do what it said.
/// </para>
/// </remarks>
public sealed class DataWipe(EleFiDbContext db, DatabaseInitialiser initialiser) : IDataWipe
{
    /// <inheritdoc />
    public async Task WipeEverythingAsync(CancellationToken cancellationToken = default)
    {
        // One transaction. A half-wiped database is worse than either outcome: some balances
        // would still be computed from rows whose containers had gone.
        await using var scope = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Child rows first: foreign keys are on, so a container cannot go before the
        // transactions naming it.
        await db.TransactionLabels.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Plans.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.GoalContainers.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Goals.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Notices.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Transactions.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.CaptureSuggestions.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Parties.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Containers.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Apps.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Labels.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await db.ParseRules.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.AuditEvents.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);

        // Back to what a fresh install looks like: the starter labels and parse rules. An
        // empty label table is legal now that unlabelled is a real state, but landing the
        // user on a blank Settings page after a wipe is not the same as a fresh install.
        await initialiser.InitialiseAsync(cancellationToken).ConfigureAwait(false);
    }
}
