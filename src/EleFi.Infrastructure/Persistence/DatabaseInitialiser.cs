using EleFi.Application.Abstractions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Labels;
using EleFi.Domain.Transactions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence;

/// <summary>
/// Brings the database up to date and seeds what the app cannot run without.
/// </summary>
/// <remarks>
/// Runs at startup, before the first render, inside a transaction. A half-migrated database
/// must never be reachable, because the screen it would render is a screen showing wrong
/// money.
/// </remarks>
public sealed class DatabaseInitialiser(EleFiDbContext db, IClock clock)
{
    /// <summary>Migrates and seeds. Safe to call on every start.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        await CopyBeforeMigratingAsync(cancellationToken).ConfigureAwait(false);
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await ReapplyAuditTriggersAsync(cancellationToken).ConfigureAwait(false);
        await SeedLabelsAsync(cancellationToken).ConfigureAwait(false);
        await SeedParseRulesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Restores the parts of a fresh install a restored backup may lack: current audit
    /// triggers and any built-in Parse Rule shipped after the backup was written.
    /// </summary>
    /// <remarks>Labels are not seeded: a backup with no labels is restored with none.</remarks>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task AfterRestoreAsync(CancellationToken cancellationToken = default)
    {
        await ReapplyAuditTriggersAsync(cancellationToken).ConfigureAwait(false);
        await SeedParseRulesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The folder, beside the database file, where copies taken before a migration are kept.</summary>
    /// <param name="databasePath">The database file.</param>
    public static string SafetyFolder(string databasePath) =>
        Path.Combine(Path.GetDirectoryName(databasePath) ?? ".", "safety");

    /// <summary>
    /// Copies the database aside before an update changes its schema.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A migration is the one moment an app update rewrites the user's data rather than just
    /// reading it. If one ever went wrong, the copy taken here is the way back. It is made with
    /// SQLite's backup API through the keyed connection, so it is consistent and encrypted with
    /// the same key, and it never leaves the app's private storage. The newest three are kept.
    /// </para>
    /// <para>
    /// Only an existing database with something pending is copied: a fresh install has nothing
    /// to protect, and an ordinary start has nothing about to change.
    /// </para>
    /// </remarks>
    private async Task CopyBeforeMigratingAsync(CancellationToken cancellationToken)
    {
        if (db.Database.GetDbConnection() is not SqliteConnection connection
            || string.IsNullOrEmpty(connection.DataSource)
            || !File.Exists(connection.DataSource))
        {
            return;
        }

        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        if (!applied.Any() || pending.Count == 0)
        {
            return;
        }

        var folder = SafetyFolder(connection.DataSource);
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, $"before-{pending[0]}.db");

        var keyed = new SqliteConnectionStringBuilder(connection.ConnectionString)
        {
            DataSource = target,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        };

        await db.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var copy = new SqliteConnection(keyed.ToString());
            copy.Open();
            connection.BackupDatabase(copy);

            // One self-contained file, not a WAL database with side files that must travel
            // with it.
            using var journal = copy.CreateCommand();
            journal.CommandText = "PRAGMA journal_mode = DELETE;";
            journal.ExecuteNonQuery();
        }
        finally
        {
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        foreach (var old in new DirectoryInfo(folder).GetFiles("before-*.db").OrderByDescending(f => f.LastWriteTimeUtc).Skip(3))
        {
            old.Delete();
        }
    }

    /// <summary>
    /// Drops and recreates the audit triggers from their current definition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A trigger has to name the columns it compares, so it is derived from the schema in
    /// the same way an index is. Pinning its text inside the migration that first created it
    /// means every column added afterwards is silently untracked until somebody remembers to
    /// write another migration, and nothing fails when they forget: the trail just quietly
    /// stops mentioning that field.
    /// </para>
    /// <para>
    /// Re-applying here makes drift impossible. It costs a few milliseconds at startup and
    /// removes a whole class of "the audit trail is missing something" bug.
    /// </para>
    /// </remarks>
    private async Task ReapplyAuditTriggersAsync(CancellationToken cancellationToken)
    {
        foreach (var statement in AuditTriggers.DropStatements)
        {
            await db.Database.ExecuteSqlRawAsync(statement, cancellationToken).ConfigureAwait(false);
        }

        foreach (var statement in AuditTriggers.CreateStatements)
        {
            await db.Database.ExecuteSqlRawAsync(statement, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Seeds a starter label set.
    /// </summary>
    /// <remarks>
    /// No Uncategorised any more. Labels are optional (ADR-0013), so a transaction with none
    /// is simply unlabelled and the breakdown gives it its own bucket. A system label that
    /// could not be deleted was a rule the user had to learn for no benefit.
    /// </remarks>
    private async Task SeedLabelsAsync(CancellationToken cancellationToken)
    {
        if (await db.Labels.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var now = clock.UtcNow;
        var seed = new (string Name, string Colour)[]
        {
            ("Food", "#f97316"),
            ("Travel", "#0ea5e9"),
            ("Shopping", "#a855f7"),
            ("Bills", "#ef4444"),
            ("Rent", "#f43f5e"),
            ("Health", "#10b981"),
            ("Salary", "#22c55e"),
            ("Investment", "#6366f1"),
        };

        var order = 0;
        foreach (var (name, colour) in seed)
        {
            db.Labels.Add(new Label
            {
                Name = name,
                Colour = colour,
                SortOrder = order++,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Seeds built-in Parse Rules for the major Indian issuers and Payment Apps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rules are data (NFR-8.11), which is the whole reason a new bank costs a row and a
    /// test rather than a class. These ship with the app and are never fetched over a
    /// network (FR-11.26): a remote channel that decides how the app reads the user's
    /// messages is a security decision, not a convenience, and it needs its own ADR.
    /// </para>
    /// <para>
    /// Additive by name rather than "only into an empty table". A rule shipped in a later
    /// release, such as the Payment App rules, reaches an install that already has the
    /// earlier ones. Built-ins already present are left exactly as they are, so a rule the
    /// user disabled stays disabled.
    /// </para>
    /// <para>
    /// The definitions live in <see cref="BuiltInParseRules"/>, beside the corpus tests that
    /// prove them, rather than here.
    /// </para>
    /// </remarks>
    private async Task SeedParseRulesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.ParseRules
            .IgnoreQueryFilters()
            .Where(r => r.IsBuiltIn)
            .Select(r => r.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var present = existing.ToHashSet(StringComparer.Ordinal);
        var now = clock.UtcNow;

        var missing = BuiltInParseRules.All
            .Where(d => !present.Contains(d.Name))
            .Select(d => BuiltInParseRules.ToRule(d, now))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        // A pattern that does not compile would silently disable itself at runtime, so it
        // is rejected at seed time instead, while there is someone to tell.
        foreach (var rule in missing)
        {
            if (!rule.TryCompile(out _, out var error))
            {
                throw new InvalidOperationException($"Built-in rule '{rule.Name}' does not compile: {error}");
            }
        }

        db.ParseRules.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
