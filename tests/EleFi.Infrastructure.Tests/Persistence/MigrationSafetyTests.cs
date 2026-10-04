using EleFi.Domain.Containers;
using EleFi.Infrastructure.Persistence;
using EleFi.Infrastructure.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EleFi.Infrastructure.Tests.Persistence;

/// <summary>
/// Migrations run on a phone holding data nobody can replace.
/// </summary>
/// <remarks>
/// A migration that throws leaves the app unable to open with the user's ledger still
/// inside it, and there is no support channel and no server copy to restore from. So the
/// interesting case is never the empty database: it is the messy one that already exists.
/// </remarks>
public class MigrationSafetyTests
{
    [Fact]
    public async Task The_unique_name_index_survives_a_database_that_already_has_duplicates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"elefi-dup-{Guid.NewGuid():N}.db");

        try
        {
            // Migrate to the schema that existed before the unique index, insert the
            // duplicates it permitted, then migrate the rest of the way. Creating the index
            // naively at this point fails with SQLITE_CONSTRAINT and the app never opens.
            await using (var db = Open(path))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("InitialSchema");

                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO Containers
                        (Id, Name, Kind, CurrencyCode, OpeningBalanceMinor, OpeningBalanceAsOf,
                         IsArchived, SortOrder, CreatedAt, UpdatedAt)
                    VALUES
                        (@a, 'HDFC', 0, 'INR', 0, '2026-01-01', 0, 0, 1000, 1000),
                        (@b, 'hdfc', 0, 'INR', 0, '2026-01-01', 0, 0, 2000, 2000),
                        (@c, 'HDFC', 0, 'INR', 0, '2026-01-01', 0, 0, 3000, 3000);
                    """,
                    new SqliteParameter("@a", Guid.CreateVersion7().ToString()),
                    new SqliteParameter("@b", Guid.CreateVersion7().ToString()),
                    new SqliteParameter("@c", Guid.CreateVersion7().ToString()));
            }

            await using (var db = Open(path))
            {
                await db.Database.MigrateAsync();

                var names = await db.Containers.Select(c => c.Name).OrderBy(n => n).ToListAsync();

                // Nothing deleted and nothing merged: two containers sharing a name may well
                // be two real accounts, and only the person who made them knows.
                Assert.Equal(3, names.Count);
                Assert.Equal(3, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());

                // The oldest keeps the name it had. Renaming the one the user has been
                // looking at for months would be the more confusing choice.
                Assert.Contains("HDFC", names, StringComparer.Ordinal);
                Assert.Contains("hdfc (2)", names, StringComparer.OrdinalIgnoreCase);
                Assert.Contains("HDFC (3)", names, StringComparer.OrdinalIgnoreCase);
            }
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Moving_to_many_labels_carries_the_existing_assignment_across()
    {
        var path = Path.Combine(Path.GetTempPath(), $"elefi-labels-{Guid.NewGuid():N}.db");

        var foodId = Guid.CreateVersion7().ToString();
        var uncategorisedId = Guid.CreateVersion7().ToString();

        try
        {
            await using (var db = Open(path))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("TransactionTimeInvestmentKindsAndUniqueContainerNames");

                await SeedOldShapeAsync(db, foodId, uncategorisedId);
            }

            await using (var db = Open(path))
            {
                await db.Database.MigrateAsync();

                // Found by amount rather than id, so the test does not depend on how the
                // provider happens to render a Guid as text.
                var labelled = await db.Transactions
                    .Include(t => t.Labels).ThenInclude(l => l.Label)
                    .FirstAsync(t => t.SourceAmountMinor == 45000);

                // The user's real choice survives. Losing it would be silent: the amounts
                // would all still be right, and only the reports would quietly change.
                var label = Assert.Single(labelled.Labels);
                Assert.Equal("Food", label.Label!.Name);

                // The one that only ever carried the system label becomes unlabelled.
                // Nobody chose Uncategorised; capture assigned it because the old model
                // demanded a label, so carrying it over would tag all of history.
                var untagged = await db.Transactions
                    .Include(t => t.Labels)
                    .FirstAsync(t => t.SourceAmountMinor == 10000);

                Assert.Empty(untagged.Labels);
                Assert.DoesNotContain(await db.Labels.ToListAsync(), l => l.Name == "Uncategorised");
            }
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Every_migration_replays_on_an_empty_database_one_at_a_time()
    {
        var path = Path.Combine(Path.GetTempPath(), $"elefi-replay-{Guid.NewGuid():N}.db");

        try
        {
            await using var db = Open(path);
            var migrator = db.Database.GetService<IMigrator>();

            // One at a time, in order. A migration that reaches into live code is running
            // today's definition against the schema as it stood back then, and only shows up
            // this way: the audit triggers grew a pair referencing TransactionLabels, and the
            // very first migration began failing with "no such table" three migrations before
            // that table exists.
            foreach (var migration in db.Database.GetMigrations())
            {
                await migrator.MigrateAsync(migration);
            }

            var applied = await db.Database.GetAppliedMigrationsAsync();
            Assert.Equal(db.Database.GetMigrations(), applied);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task The_index_is_case_insensitive_so_the_database_agrees_with_the_service()
    {
        await using var fixture = await TestDatabase.CreateAsync();

        await fixture.ContainerService.CreateAsync(
            new EleFi.Application.Containers.CreateContainerRequest(
                "HDFC", ContainerKind.BankAccount, "INR", 0, new DateOnly(2026, 1, 1)));

        // If the index were case-sensitive, the database would happily accept this while
        // ContainerService refused it, and the two would disagree about what is legal.
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            fixture.Db.Containers.Add(new Container
            {
                Name = "hdfc",
                Kind = ContainerKind.Cash,
                CurrencyCode = "INR",
                OpeningBalanceAsOf = new DateOnly(2026, 1, 1),
                CreatedAt = fixture.Clock.UtcNow,
                UpdatedAt = fixture.Clock.UtcNow,
            });

            await fixture.Db.SaveChangesAsync();
        });
    }

    /// <summary>
    /// Writes rows in the pre-ADR-0013 shape: one nullable LabelId, a system label.
    /// </summary>
    /// <remarks>
    /// Raw SQL, because the C# model no longer has these columns. That is the only honest
    /// way to test a migration: the old shape exists on the user's phone, not in the code.
    /// </remarks>
    private static async Task SeedOldShapeAsync(EleFiDbContext db, string foodId, string uncategorisedId)
    {
        // Every id is a real Guid rendered by Guid.ToString(), which is exactly what EF's
        // SQLite provider writes. Hand-made literals like 'p0000000-...' look like GUIDs but
        // contain non-hex characters, so nothing would ever match them on read.
        var container = Guid.CreateVersion7().ToString();
        var containerParty = Guid.CreateVersion7().ToString();
        var externalParty = Guid.CreateVersion7().ToString();

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Labels (Id, Name, AppliesTo, IsSystem, SortOrder, CreatedAt, UpdatedAt)
            VALUES (@food, 'Food', 0, 0, 0, 1000, 1000),
                   (@unc, 'Uncategorised', 2, 1, 1, 1000, 1000);

            INSERT INTO Containers
                (Id, Name, Kind, CurrencyCode, OpeningBalanceMinor, OpeningBalanceAsOf,
                 IsArchived, SortOrder, CreatedAt, UpdatedAt)
            VALUES (@container, 'HDFC', 0, 'INR', 0, '2026-01-01', 0, 0, 1000, 1000);

            INSERT INTO Parties (Id, Kind, ContainerId, UsageCount, CreatedAt, UpdatedAt)
            VALUES (@containerParty, 0, @container, 0, 1000, 1000);

            INSERT INTO Parties (Id, Kind, Name, UsageCount, CreatedAt, UpdatedAt)
            VALUES (@externalParty, 1, 'Zomato', 0, 1000, 1000);

            INSERT INTO Transactions
                (Id, SourcePartyId, DestinationPartyId, SourceAmountMinor, SourceCurrencyCode,
                 DestinationAmountMinor, DestinationCurrencyCode, OccurredOn, LabelId,
                 NeedsReview, CaptureSource, CreatedAt, UpdatedAt)
            VALUES
                (@txn, @containerParty, @externalParty,
                 45000, 'INR', 45000, 'INR', '2026-08-30', @food, 0, 0, 1000, 1000),
                (@untagged, @containerParty, @externalParty,
                 10000, 'INR', 10000, 'INR', '2026-08-30', @unc, 0, 0, 1000, 1000);
            """,
            new SqliteParameter("@food", foodId),
            new SqliteParameter("@unc", uncategorisedId),
            new SqliteParameter("@container", container),
            new SqliteParameter("@containerParty", containerParty),
            new SqliteParameter("@externalParty", externalParty),
            new SqliteParameter("@txn", Guid.CreateVersion7().ToString()),
            new SqliteParameter("@untagged", Guid.CreateVersion7().ToString()));
    }

    private static EleFiDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<EleFiDbContext>().UseSqlite(ConnectionStringFor(path)).Options);

    private static string ConnectionStringFor(string path) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = "test-key-not-a-real-one",
            ForeignKeys = true,
        }.ToString();

    /// <summary>
    /// Releases this database's pooled connections, then deletes the file.
    /// </summary>
    /// <remarks>
    /// This pool only, never <c>ClearAllPools</c>. The pool is process-wide, so clearing all
    /// of them shuts connections belonging to tests still running in parallel and one of them
    /// then fails to reopen, seemingly at random.
    /// </remarks>
    private static void Cleanup(string path)
    {
        using (var pooled = new SqliteConnection(ConnectionStringFor(path)))
        {
            SqliteConnection.ClearPool(pooled);
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a test over.
        }
    }
}
