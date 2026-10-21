using EleFi.Application.Abstractions;
using EleFi.Domain.Receipts;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Persistence.Repositories;

/// <summary>
/// Receipts and their photos. Photos are read only by the two methods that are about to show
/// one, so listing a transaction's receipts never loads a megabyte it does not need.
/// </summary>
public sealed class ReceiptRepository(EleFiDbContext db, IClock clock) : IReceiptRepository
{
    /// <inheritdoc />
    public async Task AddAsync(Receipt receipt, ReceiptImage? image, CancellationToken cancellationToken = default)
    {
        db.Receipts.Add(receipt);
        if (image is not null)
        {
            db.ReceiptImages.Add(image);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Receipt?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Receipts.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Receipt>> ForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        await db.Receipts.AsNoTracking()
            .Where(r => r.TransactionId == transactionId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, byte[]>> ThumbnailsAsync(IReadOnlyCollection<Guid> receiptIds, CancellationToken cancellationToken = default)
    {
        var ids = receiptIds.ToList();
        return await db.ReceiptImages.AsNoTracking()
            .Where(i => ids.Contains(i.ReceiptId))
            .Select(i => new { i.ReceiptId, i.Thumbnail })
            .ToDictionaryAsync(i => i.ReceiptId, i => i.Thumbnail, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<byte[]?> PhotoAsync(Guid receiptId, CancellationToken cancellationToken = default) =>
        db.ReceiptImages.AsNoTracking()
            .Where(i => i.ReceiptId == receiptId)
            .Select(i => i.Photo)
            .FirstOrDefaultAsync(cancellationToken)!;

    /// <inheritdoc />
    public async Task UpdateAsync(Receipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        receipt.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await db.Receipts.FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false) is { } receipt)
        {
            receipt.DeletedAt = receipt.UpdatedAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters, because a removed receipt is filtered out by definition.
        if (await db.Receipts.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false) is { } receipt)
        {
            receipt.DeletedAt = null;
            receipt.UpdatedAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
