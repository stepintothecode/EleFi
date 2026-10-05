using EleFi.Domain.Alerts;
using EleFi.Domain.Apps;
using EleFi.Domain.Audit;
using EleFi.Domain.Containers;
using EleFi.Domain.Labels;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence;

/// <summary>
/// The local database. The single source of truth, encrypted at rest by SQLCipher.
/// </summary>
/// <remarks>
/// <para>
/// Not a cache and not a mirror of anything. EleFi has no server that holds user data, so
/// this file is the ledger.
/// </para>
/// <para>
/// Two rules are enforced here once rather than remembered per query: soft-deleted rows are
/// excluded by a global query filter, and money is stored as integers. A forgotten
/// <c>DeletedAt</c> filter would leak a deleted transaction into a balance, which is
/// exactly the silent wrongness this design exists to prevent.
/// </para>
/// </remarks>
public class EleFiDbContext(DbContextOptions<EleFiDbContext> options) : DbContext(options)
{
    /// <summary>Money Containers.</summary>
    public DbSet<Container> Containers => Set<Container>();

    /// <summary>Parties: the user's containers and everyone outside.</summary>
    public DbSet<Party> Parties => Set<Party>();

    /// <summary>Transactions.</summary>
    public DbSet<Transaction> Transactions => Set<Transaction>();

    /// <summary>The join between transactions and labels.</summary>
    public DbSet<TransactionLabel> TransactionLabels => Set<TransactionLabel>();

    /// <summary>Labels.</summary>
    public DbSet<Label> Labels => Set<Label>();

    /// <summary>Apps: marketplaces and payment rails, one pool.</summary>
    public DbSet<App> Apps => Set<App>();

    /// <summary>SMS Parse Rules.</summary>
    public DbSet<ParseRule> ParseRules => Set<ParseRule>();

    /// <summary>Capture Suggestions awaiting confirmation.</summary>
    public DbSet<CaptureSuggestion> CaptureSuggestions => Set<CaptureSuggestion>();

    /// <summary>The append-only audit trail, written by triggers.</summary>
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>Plans: money the user expects to move.</summary>
    public DbSet<Domain.Planning.Plan> Plans => Set<Domain.Planning.Plan>();

    /// <summary>The in-app notification list.</summary>
    public DbSet<Domain.Notices.Notice> Notices => Set<Domain.Notices.Notice>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        // A calendar day is stored as YYYY-MM-DD text, not a timestamp. Storing it as an
        // instant introduces a timezone bug the day the user crosses one, and the
        // transaction appears to move.
        var dateOnly = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateOnly, string>(
            d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            s => DateOnly.ParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        var nullableDateOnly = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateOnly?, string?>(
            d => d == null ? null : d.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            s => s == null ? null : DateOnly.ParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        // Wall-clock time, stored as HH:mm. Not an offset and not an instant: it is the
        // time the user would have read off a clock, which is what they remember.
        var nullableTimeOnly = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<TimeOnly?, string?>(
            t => t == null ? null : t.Value.ToString("HH\\:mm", System.Globalization.CultureInfo.InvariantCulture),
            s => s == null ? null : TimeOnly.ParseExact(s, "HH\\:mm", System.Globalization.CultureInfo.InvariantCulture));

        // An instant is epoch milliseconds: unambiguous, sortable, and timezone-safe.
        var instant = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, long>(
            d => d.ToUnixTimeMilliseconds(),
            l => DateTimeOffset.FromUnixTimeMilliseconds(l));

        var nullableInstant = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset?, long?>(
            d => d == null ? null : d.Value.ToUnixTimeMilliseconds(),
            l => l == null ? null : DateTimeOffset.FromUnixTimeMilliseconds(l.Value));

        modelBuilder.Entity<Container>(e =>
        {
            e.ToTable("Containers");
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(120);
            e.Property(c => c.CurrencyCode).IsRequired().HasMaxLength(3);
            e.Property(c => c.OpeningBalanceAsOf).HasConversion(dateOnly);
            e.Property(c => c.MaturityDate).HasConversion(nullableDateOnly);
            e.Property(c => c.CreatedAt).HasConversion(instant);
            e.Property(c => c.UpdatedAt).HasConversion(instant);
            e.Property(c => c.DeletedAt).HasConversion(nullableInstant);

            // C1: the column itself is four characters wide. Even a bug that tried to write
            // a full account number could not fit one.
            e.Property(c => c.AccountNumberLast4).HasMaxLength(4);

            e.HasQueryFilter(c => c.DeletedAt == null);
            e.HasIndex(c => c.AccountNumberLast4);

            // Names are unique among live containers, case-insensitively. Two containers
            // called "HDFC" are indistinguishable in every picker, every filter chip, and
            // every export row, so the user cannot tell which one a transaction touched.
            //
            // Filtered on DeletedAt so a deleted container does not reserve its name
            // forever, which would be a strange thing to discover months later.
            e.HasIndex(c => c.Name)
                .IsUnique()
                .HasFilter("DeletedAt IS NULL");
        });

        modelBuilder.Entity<Party>(e =>
        {
            e.ToTable("Parties");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.CreatedAt).HasConversion(instant);
            e.Property(p => p.UpdatedAt).HasConversion(instant);
            e.Property(p => p.DeletedAt).HasConversion(nullableInstant);
            e.Property(p => p.LastUsedAt).HasConversion(nullableInstant);
            e.HasQueryFilter(p => p.DeletedAt == null);
            e.HasIndex(p => p.ContainerId);
            e.HasIndex(p => p.Name);
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("Transactions");
            e.HasKey(t => t.Id);
            e.Property(t => t.OccurredOn).HasConversion(dateOnly).IsRequired();
            e.Property(t => t.OccurredAtTime).HasConversion(nullableTimeOnly).HasMaxLength(5);
            e.Property(t => t.SourceCurrencyCode).IsRequired().HasMaxLength(3);
            e.Property(t => t.DestinationCurrencyCode).IsRequired().HasMaxLength(3);
            e.Property(t => t.Description).HasMaxLength(500);
            e.Property(t => t.CreatedAt).HasConversion(instant);
            e.Property(t => t.UpdatedAt).HasConversion(instant);
            e.Property(t => t.DeletedAt).HasConversion(nullableInstant);

            e.HasOne(t => t.SourceParty).WithMany().HasForeignKey(t => t.SourcePartyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.DestinationParty).WithMany().HasForeignKey(t => t.DestinationPartyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.MarketplaceApp).WithMany().HasForeignKey(t => t.MarketplaceAppId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.PaymentApp).WithMany().HasForeignKey(t => t.PaymentAppId).OnDelete(DeleteBehavior.SetNull);

            e.HasQueryFilter(t => t.DeletedAt == null);

            // The indexes the list and the balance query actually use. A heavy decade is
            // about 40,000 rows, which SQLite sums in single-digit milliseconds given these.
            e.HasIndex(t => t.OccurredOn);
            e.HasIndex(t => t.SourcePartyId);
            e.HasIndex(t => t.DestinationPartyId);

            e.ToTable(t =>
            {
                // T3: zero-amount transactions are meaningless and negatives are prohibited
                // by M4. Enforced in the schema so no code path can write one.
                t.HasCheckConstraint("CK_Transactions_PositiveAmounts",
                    "SourceAmountMinor > 0 AND DestinationAmountMinor > 0");

                // T4: same currency means the amounts must match, so a same-currency
                // transaction cannot lose or invent money.
                t.HasCheckConstraint("CK_Transactions_SameCurrencySameAmount",
                    "SourceCurrencyCode <> DestinationCurrencyCode OR SourceAmountMinor = DestinationAmountMinor");

                // T2: money cannot move to where it already is.
                t.HasCheckConstraint("CK_Transactions_DistinctParties",
                    "SourcePartyId <> DestinationPartyId");
            });
        });

        modelBuilder.Entity<TransactionLabel>(e =>
        {
            e.ToTable("TransactionLabels");
            e.HasKey(tl => new { tl.TransactionId, tl.LabelId });
            e.HasOne(tl => tl.Label).WithMany().HasForeignKey(tl => tl.LabelId).OnDelete(DeleteBehavior.Cascade);

            // Both directions get an index: the list filters by label, and every row render
            // loads the labels for a transaction.
            e.HasIndex(tl => tl.LabelId);
        });

        modelBuilder.Entity<Label>(e =>
        {
            e.ToTable("Labels");
            e.HasKey(l => l.Id);
            e.Property(l => l.Name).IsRequired().HasMaxLength(80);
            e.Property(l => l.CreatedAt).HasConversion(instant);
            e.Property(l => l.UpdatedAt).HasConversion(instant);
            e.Property(l => l.DeletedAt).HasConversion(nullableInstant);
            e.HasQueryFilter(l => l.DeletedAt == null);

            // Flat and unique. With no hierarchy there is no "unique per parent" to fall
            // back on, so two labels called Food would simply be indistinguishable.
            e.HasIndex(l => l.Name).IsUnique().HasFilter("DeletedAt IS NULL");
        });

        modelBuilder.Entity<App>(e =>
        {
            e.ToTable("Apps");
            e.HasKey(a => a.Id);
            e.Property(a => a.Name).IsRequired().HasMaxLength(80);
            e.Property(a => a.CreatedAt).HasConversion(instant);
            e.Property(a => a.UpdatedAt).HasConversion(instant);
            e.Property(a => a.DeletedAt).HasConversion(nullableInstant);
            e.Property(a => a.LastUsedAt).HasConversion(nullableInstant);
            e.HasQueryFilter(a => a.DeletedAt == null);
        });

        modelBuilder.Entity<ParseRule>(e =>
        {
            e.ToTable("ParseRules");
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).IsRequired().HasMaxLength(120);
            e.Property(r => r.SenderPattern).IsRequired().HasMaxLength(200);
            e.Property(r => r.BodyPattern).IsRequired().HasMaxLength(1000);
            e.Property(r => r.AppName).HasMaxLength(80);
            e.Property(r => r.CreatedAt).HasConversion(instant);
            e.Property(r => r.UpdatedAt).HasConversion(instant);
            e.Property(r => r.DeletedAt).HasConversion(nullableInstant);
            e.HasQueryFilter(r => r.DeletedAt == null);
        });

        modelBuilder.Entity<CaptureSuggestion>(e =>
        {
            e.ToTable("CaptureSuggestions");
            e.HasKey(s => s.Id);
            e.Property(s => s.Fingerprint).IsRequired().HasMaxLength(64);
            e.Property(s => s.CurrencyCode).IsRequired().HasMaxLength(3);
            e.Property(s => s.CounterpartyText).HasMaxLength(200);
            e.Property(s => s.OccurredOn).HasConversion(nullableDateOnly);
            e.Property(s => s.CreatedAt).HasConversion(instant);
            e.Property(s => s.ExpiresAt).HasConversion(instant);
            e.Property(s => s.PaymentAppName).HasMaxLength(80);
            e.Property(s => s.Note).HasMaxLength(200);
            e.Property(s => s.CorroboratingFingerprint).HasMaxLength(64);
            e.Property(s => s.OccurredAtTime).HasConversion(nullableTimeOnly).HasMaxLength(5);
            e.HasIndex(s => s.CreatedAt);

            // SM6: one live suggestion per fingerprint, so a re-delivered alert cannot
            // produce a second offer for the same payment.
            e.HasIndex(s => s.Fingerprint).IsUnique();
            e.HasIndex(s => s.CorroboratingFingerprint);

            // No soft-delete filter and no DeletedAt column, on purpose. A dismissed
            // suggestion is hard-deleted (SM5): it is machine output the user rejected,
            // not user-entered data that soft delete exists to protect.
        });

        modelBuilder.Entity<Domain.Planning.Plan>(e =>
        {
            e.ToTable("Plans");
            e.HasKey(p => p.Id);
            e.Property(p => p.Title).IsRequired().HasMaxLength(120);
            e.Property(p => p.Note).HasMaxLength(500);
            e.Property(p => p.CurrencyCode).IsRequired().HasMaxLength(3);
            e.Property(p => p.CounterpartyName).HasMaxLength(200);
            e.Property(p => p.DueOn).HasConversion(dateOnly).IsRequired();
            e.Property(p => p.DueTime).HasConversion(nullableTimeOnly).HasMaxLength(5);
            e.Property(p => p.CompletedAt).HasConversion(nullableInstant);
            e.Property(p => p.CreatedAt).HasConversion(instant);
            e.Property(p => p.UpdatedAt).HasConversion(instant);
            e.Property(p => p.DeletedAt).HasConversion(nullableInstant);
            e.Ignore(p => p.Repeat);

            // A plan is never money (D2): no foreign key ties it to a balance, only to the
            // transaction it was ticked off against.
            e.HasQueryFilter(p => p.DeletedAt == null);
            e.HasIndex(p => p.DueOn);
            e.HasIndex(p => p.TransactionId);
            e.HasIndex(p => p.SeriesId);
        });

        modelBuilder.Entity<Domain.Notices.Notice>(e =>
        {
            e.ToTable("Notices");
            e.HasKey(n => n.Id);
            e.Property(n => n.Title).IsRequired().HasMaxLength(200);
            e.Property(n => n.Body).IsRequired().HasMaxLength(1000);
            e.Property(n => n.Route).HasMaxLength(200);
            e.Property(n => n.ReadAt).HasConversion(nullableInstant);
            e.Property(n => n.CreatedAt).HasConversion(instant);
            e.Property(n => n.UpdatedAt).HasConversion(instant);
            e.Property(n => n.DeletedAt).HasConversion(nullableInstant);
            e.HasQueryFilter(n => n.DeletedAt == null);
            e.HasIndex(n => n.Route);
            e.HasIndex(n => n.CreatedAt);
        });

        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.ToTable("AuditEvents");
            e.HasKey(a => a.Id);
            e.Property(a => a.EntityType).IsRequired().HasMaxLength(60);
            e.Property(a => a.OccurredAt).HasConversion(instant);
            e.HasIndex(a => new { a.EntityType, a.EntityId });
        });
    }
}
