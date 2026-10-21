using EleFi.Domain.Receipts;
using EleFi.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EleFi.Infrastructure.Persistence.Configurations;

/// <summary>How receipts are stored (ADR-0018).</summary>
public sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    private static readonly ValueConverter<DateTimeOffset, long> Instant = new(d => d.ToUnixTimeMilliseconds(), l => DateTimeOffset.FromUnixTimeMilliseconds(l));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableInstant =
        new(d => d == null ? null : d.Value.ToUnixTimeMilliseconds(), l => l == null ? null : DateTimeOffset.FromUnixTimeMilliseconds(l.Value));

    private static readonly ValueConverter<DateOnly?, string?> Day = new(
        d => d == null ? null : d.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        s => s == null ? null : DateOnly.ParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

    private static readonly ValueConverter<TimeOnly?, string?> Time = new(
        t => t == null ? null : t.Value.ToString("HH\\:mm", System.Globalization.CultureInfo.InvariantCulture),
        s => s == null ? null : TimeOnly.ParseExact(s, "HH\\:mm", System.Globalization.CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Receipts", t =>
        {
            // RC2: a total of zero or less is a misreading, never a receipt.
            t.HasCheckConstraint("CK_Receipts_PositiveAmount", "AmountMinor IS NULL OR AmountMinor > 0");

            // C1 enforced: SQLite does not hold a TEXT column to its declared length, so the
            // four digits are a CHECK, and a full account number cannot be stored here at all.
            t.HasCheckConstraint("CK_Receipts_AccountLast4", "AccountLast4 IS NULL OR length(AccountLast4) = 4");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.AppName).HasMaxLength(80);
        builder.Property(r => r.Payee).HasMaxLength(Receipt.MaxPayeeLength);
        builder.Property(r => r.CurrencyCode).IsRequired().HasMaxLength(3);
        builder.Property(r => r.Reference).HasMaxLength(Receipt.MaxReferenceLength);
        builder.Property(r => r.Note).HasMaxLength(Receipt.MaxNoteLength);

        builder.Property(r => r.AccountLast4).HasMaxLength(4);
        builder.Property(r => r.OccurredOn).HasConversion(Day);
        builder.Property(r => r.OccurredAtTime).HasConversion(Time).HasMaxLength(5);
        builder.Property(r => r.SettledAt).HasConversion(NullableInstant);
        builder.Property(r => r.CreatedAt).HasConversion(Instant);
        builder.Property(r => r.UpdatedAt).HasConversion(Instant);
        builder.Property(r => r.DeletedAt).HasConversion(NullableInstant);
        builder.Ignore(r => r.Facts);

        builder.HasOne<Transaction>().WithMany().HasForeignKey(r => r.TransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(r => r.DeletedAt == null);
        builder.HasIndex(r => r.TransactionId);

        // A receipt shared twice is found by its reference.
        builder.HasIndex(r => r.Reference);
    }
}

/// <summary>
/// How receipt photos are stored: in their own table, so nothing reads a photo it is not
/// about to show.
/// </summary>
public sealed class ReceiptImageConfiguration : IEntityTypeConfiguration<ReceiptImage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReceiptImage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReceiptImages");
        builder.HasKey(i => i.ReceiptId);
        builder.Property(i => i.Photo).IsRequired();
        builder.Property(i => i.Thumbnail).IsRequired();
        builder.HasOne<Receipt>().WithOne().HasForeignKey<ReceiptImage>(i => i.ReceiptId).OnDelete(DeleteBehavior.Cascade);
    }
}
