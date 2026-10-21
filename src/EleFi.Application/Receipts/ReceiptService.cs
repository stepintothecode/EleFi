using EleFi.Application.Abstractions;
using EleFi.Application.Transactions;
using EleFi.Domain.Parties;
using EleFi.Domain.Receipts;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Receipts;

/// <summary>A receipt as its transaction shows it: what it says, how it compares, its thumbnail.</summary>
/// <param name="Receipt">The receipt.</param>
/// <param name="Discrepancy">What it disagrees with the transaction on, settled or not.</param>
/// <param name="Thumbnail">A small JPEG, or null with no photo.</param>
public sealed record ReceiptView(Receipt Receipt, Discrepancy Discrepancy, byte[]? Thumbnail)
{
    /// <summary>True while a disagreement has not been settled.</summary>
    public bool NeedsChecking => Discrepancy != Discrepancy.None && Receipt.SettledAt is null;
}

/// <summary>What attaching a receipt came to.</summary>
/// <param name="Transaction">The transaction it is attached to.</param>
/// <param name="Discrepancy">What the receipt disagrees with it on.</param>
/// <param name="Error">Why it was refused, when it was.</param>
public readonly record struct ReceiptOutcome(Transaction? Transaction, Discrepancy Discrepancy, string? Error)
{
    /// <summary>True when it was attached.</summary>
    public bool Succeeded => Error is null;
}

/// <summary>
/// Attaches receipts to transactions, records a new transaction for one that matches nothing,
/// and settles or removes them (ADR-0018).
/// </summary>
/// <remarks>
/// A receipt that disagrees with its transaction flags the transaction Needs Review, so it
/// surfaces where everything else to check does. A receipt that matches nothing becomes a new
/// transaction flagged the same way, as an alert would (ADR-0015).
/// </remarks>
public sealed class ReceiptService(
    IReceiptRepository receipts,
    ITransactionRepository transactions,
    IContainerRepository containers,
    IPartyRepository parties,
    IContactRepository contacts,
    CaptureService capture,
    EditTransactionService editing,
    IClock clock)
{
    /// <summary>
    /// Attaches a receipt, and when asked, makes the items read from it the transaction's items.
    /// </summary>
    /// <param name="transactionId">The transaction.</param>
    /// <param name="reading">What the receipt says, as the user left it.</param>
    /// <param name="scan">The photo, when there is one.</param>
    /// <param name="useItems">Line items to itemise the transaction with; none to leave its items alone.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ReceiptOutcome> AttachAsync(
        Guid transactionId,
        ReceiptReading reading,
        ScannedReceipt? scan,
        IReadOnlyList<ReadItem> useItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(useItems);

        var transaction = await transactions.FindAsync(transactionId, cancellationToken).ConfigureAwait(false);
        if (transaction is null)
        {
            return new ReceiptOutcome(null, Discrepancy.None, "That transaction no longer exists.");
        }

        if (useItems.Count > 0)
        {
            var items = useItems.Select(i => ItemDraft.Named(i.Name, i.AmountMinor)).ToList();
            var itemised = await editing.EditAsync(EditRequest.Snapshot(transaction) with { Items = items }, cancellationToken).ConfigureAwait(false);
            if (!itemised.Succeeded)
            {
                return new ReceiptOutcome(null, Discrepancy.None, itemised.Error);
            }
        }

        var receipt = Receipt.From(reading, transactionId);
        receipt.CreatedAt = receipt.UpdatedAt = clock.UtcNow;
        await receipts.AddAsync(receipt, ImageOf(receipt.Id, scan), cancellationToken).ConfigureAwait(false);

        var found = await CheckAsync(receipt, transaction, cancellationToken).ConfigureAwait(false);
        if (found != Discrepancy.None && !transaction.NeedsReview)
        {
            transaction.NeedsReview = true;
            await transactions.UpdateAsync(transaction, cancellationToken).ConfigureAwait(false);
        }

        return new ReceiptOutcome(transaction, found, null);
    }

    /// <summary>
    /// Records a new transaction from a receipt that matches none, flagged Needs Review, and
    /// attaches the receipt to it.
    /// </summary>
    /// <remarks>
    /// Paid from the container whose last four digits the receipt shows, or the first container
    /// in picker order when it shows none: a guess, which is what the review flag is for.
    /// </remarks>
    /// <param name="reading">What the receipt says.</param>
    /// <param name="scan">The photo, when there is one.</param>
    /// <param name="useItems">Line items to itemise it with.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ReceiptOutcome> RecordAsync(
        ReceiptReading reading,
        ScannedReceipt? scan,
        IReadOnlyList<ReadItem> useItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (reading.Facts.AmountMinor is not { } amount)
        {
            return new ReceiptOutcome(null, Discrepancy.None, "There is no amount on that receipt to record.");
        }

        var from = await PaidFromAsync(reading, cancellationToken).ConfigureAwait(false);
        if (from is null)
        {
            return new ReceiptOutcome(null, Discrepancy.None, "Add a container first, so EleFi knows where the money came from.");
        }

        var payee = await parties.GetOrCreateExternalAsync(reading.Facts.Payee ?? "Unknown payee", cancellationToken).ConfigureAwait(false);
        var currency = Domain.Money.Currency.Inr.Code;
        var captured = await capture.CaptureAsync(
            new CaptureRequest(
                from.Id, payee.Id, amount, currency, amount, currency,
                reading.Facts.OccurredOn ?? clock.Today, reading.OccurredAtTime,
                Description: reading.Note, NeedsReview: true, CaptureSource: CaptureSource.ShareSheet),
            cancellationToken).ConfigureAwait(false);

        return captured.Succeeded
            ? await AttachAsync(captured.Transaction!.Id, reading, scan, useItems, cancellationToken).ConfigureAwait(false)
            : new ReceiptOutcome(null, Discrepancy.None, captured.Error);
    }

    /// <summary>A transaction's receipts, each with how it compares and its thumbnail.</summary>
    /// <param name="transactionId">The transaction.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<ReceiptView>> ForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var attached = await receipts.ForTransactionAsync(transactionId, cancellationToken).ConfigureAwait(false);
        if (attached.Count == 0)
        {
            return [];
        }

        var transaction = await transactions.FindAsync(transactionId, cancellationToken).ConfigureAwait(false);
        var thumbnails = await receipts.ThumbnailsAsync([.. attached.Select(r => r.Id)], cancellationToken).ConfigureAwait(false);

        var views = new List<ReceiptView>(attached.Count);
        foreach (var receipt in attached)
        {
            var found = transaction is null ? Discrepancy.None : await CheckAsync(receipt, transaction, cancellationToken).ConfigureAwait(false);
            views.Add(new ReceiptView(receipt, found, thumbnails.GetValueOrDefault(receipt.Id)));
        }

        return views;
    }

    /// <summary>A receipt's full photo, or null when it has none.</summary>
    /// <param name="receiptId">The receipt.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<byte[]?> PhotoAsync(Guid receiptId, CancellationToken cancellationToken = default) =>
        receipts.PhotoAsync(receiptId, cancellationToken);

    /// <summary>Keeps the transaction as it is despite what the receipt says (RC5).</summary>
    /// <param name="receiptId">The receipt.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task SettleAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        if (await receipts.FindAsync(receiptId, cancellationToken).ConfigureAwait(false) is { } receipt)
        {
            receipt.SettledAt = receipt.UpdatedAt = clock.UtcNow;
            await receipts.UpdateAsync(receipt, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Makes the transaction's amount the receipt's, through the same checked edit as any.</summary>
    /// <param name="receiptId">The receipt.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<CaptureResult> UseAmountAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var receipt = await receipts.FindAsync(receiptId, cancellationToken).ConfigureAwait(false);
        var transaction = receipt is null ? null : await transactions.FindAsync(receipt.TransactionId, cancellationToken).ConfigureAwait(false);
        if (receipt?.AmountMinor is not { } amount || transaction is null)
        {
            return CaptureResult.Fail("That receipt or its transaction no longer exists.");
        }

        return await editing.EditAsync(EditRequest.Snapshot(transaction) with { AmountMinor = amount, Items = null }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Soft-deletes a receipt.</summary>
    /// <param name="receiptId">The receipt.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task RemoveAsync(Guid receiptId, CancellationToken cancellationToken = default) =>
        receipts.SoftDeleteAsync(receiptId, cancellationToken);

    /// <summary>Brings a removed receipt back, for undo.</summary>
    /// <param name="receiptId">The receipt.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task RestoreAsync(Guid receiptId, CancellationToken cancellationToken = default) =>
        receipts.RestoreAsync(receiptId, cancellationToken);

    /// <summary>What a receipt disagrees with a transaction on, knowing its account and its payee's other names.</summary>
    /// <param name="receipt">The receipt.</param>
    /// <param name="transaction">The transaction, with parties loaded.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<Discrepancy> CheckAsync(Receipt receipt, Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return await CheckAsync(receipt.Facts, transaction, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>What a receipt's facts disagree with a transaction on, before anything is attached.</summary>
    /// <param name="facts">What the receipt says.</param>
    /// <param name="transaction">The transaction, with parties loaded.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<Discrepancy> CheckAsync(ReceiptFacts facts, Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(transaction);

        var own = transaction.SourceParty is { Kind: PartyKind.Container } s ? s : transaction.DestinationParty;
        var container = own?.ContainerId is { } containerId
            ? await containers.FindAsync(containerId, cancellationToken).ConfigureAwait(false)
            : null;

        var other = transaction.DestinationParty is { Kind: PartyKind.External } d ? d : transaction.SourceParty;
        var aliases = other is { Kind: PartyKind.External }
            ? await contacts.AliasesAsync(other.Id, cancellationToken).ConfigureAwait(false)
            : [];

        return ReceiptCheck.Compare(facts, transaction, container?.AccountNumberLast4, aliases);
    }

    private async Task<Party?> PaidFromAsync(ReceiptReading reading, CancellationToken cancellationToken)
    {
        if (reading.Facts.AccountLast4 is { } last4
            && (await containers.FindByLast4Async(last4, cancellationToken).ConfigureAwait(false)) is [var byCard, ..])
        {
            return await containers.PartyForAsync(byCard.Id, cancellationToken).ConfigureAwait(false);
        }

        var first = Domain.Containers.ContainerGrouping.InPickerOrder(await containers.ListSelectableAsync(cancellationToken).ConfigureAwait(false));
        return first.Count == 0 ? null : await containers.PartyForAsync(first[0].Id, cancellationToken).ConfigureAwait(false);
    }

    private static ReceiptImage? ImageOf(Guid receiptId, ScannedReceipt? scan) =>
        scan is { Photo: { } photo }
            ? new ReceiptImage { ReceiptId = receiptId, Photo = photo, Thumbnail = scan.Thumbnail ?? [] }
            : null;
}
