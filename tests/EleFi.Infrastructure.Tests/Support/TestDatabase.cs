using EleFi.Application.Abstractions;
using EleFi.Application.Containers;
using EleFi.Application.Export;
using EleFi.Application.Suggestions;
using EleFi.Application.Transactions;
using EleFi.Infrastructure.Persistence;
using EleFi.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Tests.Support;

/// <summary>
/// A real, encrypted, migrated database in a temporary file, for one test.
/// </summary>
/// <remarks>
/// <para>
/// A file rather than <c>:memory:</c> on purpose: SQLCipher keys a file, and an in-memory
/// database would test a code path the app never runs. It is also the only way the
/// migration and its triggers get exercised at all.
/// </para>
/// <para>
/// The database is not mocked. Mocking a database mostly tests the mock, and the invariants
/// that matter here (CHECK constraints, triggers, query filters) live in the database
/// rather than in the code that calls it.
/// </para>
/// </remarks>
internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _path;
    private readonly string _connectionString;

    private TestDatabase(string path, string connectionString, EleFiDbContext db, FixedClock clock)
    {
        _path = path;
        _connectionString = connectionString;
        Db = db;
        Clock = clock;
    }

    public EleFiDbContext Db { get; }

    public FixedClock Clock { get; }

    public ContainerRepository Containers => new(Db, Clock);

    public TransactionRepository Transactions => new(Db, Clock);

    public PartyRepository Parties => new(Db, Clock);

    public LabelRepository Labels => new(Db, Clock);

    public AppRepository Apps => new(Db, Clock);

    public CaptureService Capture => new(Transactions, Parties, Apps, Labels, Clock);

    public AuditRepository Audit => new(Db);

    public DataWipe Wipe => new(Db, new DatabaseInitialiser(Db, Clock));

    public LocalBackup Backup => new(Db, Wipe, Clock);

    public EditTransactionService Editing => new(Transactions, Parties, Labels, Audit, Clock);

    public ContainerService ContainerService => new(Containers, Transactions, Clock);

    public CsvExporter Exporter => new(Transactions, Clock);

    public AlertCaptureService Alerts =>
        new(new SuggestionRepository(Db), new AlertPartyResolver(Containers, Parties, Apps, Transactions), Apps, Capture, Editing, Clock);

    /// <summary>Creates, keys, migrates, and seeds a fresh database.</summary>
    public static async Task<TestDatabase> CreateAsync(DateOnly? today = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"elefi-test-{Guid.NewGuid():N}.db");

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = "test-key-not-a-real-one",
            ForeignKeys = true,
        }.ToString();

        var options = new DbContextOptionsBuilder<EleFiDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var db = new EleFiDbContext(options);
        var clock = new FixedClock(today ?? new DateOnly(2026, 8, 30));

        await new DatabaseInitialiser(db, clock).InitialiseAsync().ConfigureAwait(false);

        return new TestDatabase(path, connectionString, db, clock);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync().ConfigureAwait(false);

        // This pool only, never ClearAllPools(). The pool is keyed by connection string and
        // every test has its own file, but ClearAllPools reaches across all of them: a test
        // finishing would shut pooled connections belonging to tests still running in
        // parallel, and one of those would fail to reopen at random. Turning pooling off
        // instead is worse, because SQLCipher re-derives the key on every open.
        using (var pooled = new SqliteConnection(_connectionString))
        {
            SqliteConnection.ClearPool(pooled);
        }

        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a test over.
        }
    }
}

/// <summary>A clock that does not move, so a test never depends on when it ran.</summary>
internal sealed class FixedClock(DateOnly today) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } =
        new(today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);

    public DateOnly Today { get; } = today;

    public TimeOnly TimeOfDay { get; } = new(9, 0);

    /// <summary>Moves the clock forward, for tests that need two distinct instants.</summary>
    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
