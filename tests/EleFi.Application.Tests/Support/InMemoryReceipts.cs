using EleFi.Application.Abstractions;
using EleFi.Domain.Receipts;

namespace EleFi.Application.Tests.Support;

/// <summary>Receipts and photos in lists, for service tests.</summary>
internal sealed class InMemoryReceipts : IReceiptRepository
{
    public List<Receipt> Rows { get; } = [];

    public List<ReceiptImage> Images { get; } = [];

    public Task AddAsync(Receipt receipt, ReceiptImage? image, CancellationToken cancellationToken = default)
    {
        Rows.Add(receipt);
        if (image is not null)
        {
            Images.Add(image);
        }

        return Task.CompletedTask;
    }

    public Task<Receipt?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.Reference == reference && r.DeletedAt is null));

    public Task<Receipt?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.Id == id && r.DeletedAt is null));

    public Task<IReadOnlyList<Receipt>> ForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Receipt>>([.. Rows.Where(r => r.TransactionId == transactionId && r.DeletedAt is null)]);

    public Task<IReadOnlyDictionary<Guid, byte[]>> ThumbnailsAsync(IReadOnlyCollection<Guid> receiptIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, byte[]>>(Images.Where(i => receiptIds.Contains(i.ReceiptId)).ToDictionary(i => i.ReceiptId, i => i.Thumbnail));

    public Task<byte[]?> PhotoAsync(Guid receiptId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Images.FirstOrDefault(i => i.ReceiptId == receiptId)?.Photo);

    public Task UpdateAsync(Receipt receipt, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (Rows.FirstOrDefault(r => r.Id == id) is { } receipt)
        {
            receipt.DeletedAt = null;
        }

        return Task.CompletedTask;
    }

    public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (Rows.FirstOrDefault(r => r.Id == id) is { } receipt)
        {
            receipt.DeletedAt = DateTimeOffset.UnixEpoch;
        }

        return Task.CompletedTask;
    }
}
