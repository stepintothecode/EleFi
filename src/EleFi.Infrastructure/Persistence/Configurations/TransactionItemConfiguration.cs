using EleFi.Domain.Labels;
using EleFi.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EleFi.Infrastructure.Persistence.Configurations;

/// <summary>How a transaction's items are stored (ADR-0017).</summary>
/// <remarks>
/// IT2 is a CHECK, so no path can store an item without an amount. IT1 (they add up to the
/// transaction) spans rows, which a CHECK cannot express; it is kept by
/// <see cref="Itemisation"/> on every write, and verified by a property test.
/// </remarks>
public sealed class TransactionItemConfiguration : IEntityTypeConfiguration<TransactionItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransactionItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TransactionItems", t =>
            t.HasCheckConstraint("CK_TransactionItems_PositiveAmount", "AmountMinor > 0"));
        builder.HasKey(i => i.Id);

        // The editor gives an item its id, so an item added to a loaded transaction already has
        // one. Without this EF reads a set key as "already stored" and updates a row that is
        // not there.
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Name).HasMaxLength(TransactionItem.MaxNameLength);
        builder.Property(i => i.Note).HasMaxLength(TransactionItem.MaxNoteLength);
        builder.Ignore(i => i.IsUnnamed);

        builder.HasOne<Transaction>().WithMany(t => t.Items).HasForeignKey(i => i.TransactionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.Labels).WithOne().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Cascade);

        // Every load of an itemised transaction reads its items in order.
        builder.HasIndex(i => new { i.TransactionId, i.Position });
    }
}

/// <summary>How an item's labels are stored.</summary>
public sealed class TransactionItemLabelConfiguration : IEntityTypeConfiguration<TransactionItemLabel>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransactionItemLabel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TransactionItemLabels");
        builder.HasKey(l => new { l.ItemId, l.LabelId });
        builder.HasOne(l => l.Label).WithMany().HasForeignKey(l => l.LabelId).OnDelete(DeleteBehavior.Cascade);

        // The label filter and the breakdown look items up by label.
        builder.HasIndex(l => l.LabelId);
    }
}
