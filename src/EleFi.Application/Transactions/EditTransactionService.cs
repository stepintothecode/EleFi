using EleFi.Application.Abstractions;
using EleFi.Domain.Audit;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Transactions;

/// <summary>What an edit is changing a transaction to.</summary>
/// <param name="Id">The transaction being edited.</param>
/// <param name="SourcePartyId">The party money left.</param>
/// <param name="DestinationPartyId">The party money reached.</param>
/// <param name="AmountMinor">The amount, in minor units. Same currency on both ends for now.</param>
/// <param name="OccurredOn">The calendar day.</param>
/// <param name="OccurredAtTime">The time of day, or null.</param>
/// <param name="LabelIds">Categories. Any number, including none.</param>
/// <param name="Description">Optional free text.</param>
/// <param name="MarketplaceAppId">Optional platform bought through.</param>
/// <param name="PaymentAppId">Optional rail money moved along.</param>
/// <param name="NeedsReview">Whether it still needs checking.</param>
public sealed record EditRequest(
    Guid Id,
    Guid SourcePartyId,
    Guid DestinationPartyId,
    long AmountMinor,
    DateOnly OccurredOn,
    TimeOnly? OccurredAtTime = null,
    IReadOnlyList<Guid>? LabelIds = null,
    string? Description = null,
    Guid? MarketplaceAppId = null,
    Guid? PaymentAppId = null,
    bool NeedsReview = false);

/// <summary>
/// Edits, deletes, and restores transactions, and reads their history.
/// </summary>
/// <remarks>
/// <para>
/// Editing runs the same invariant checks as capture. A transaction that was valid when
/// written can be edited into an invalid one just as easily as a new one can be typed
/// wrong, and T1 to T7 are exactly as load-bearing on the second write as the first.
/// </para>
/// <para>
/// Nothing here destroys anything. Deleting sets <c>DeletedAt</c>, which removes the row
/// from every balance, report, and export at once (T9) while leaving it restorable. The
/// triggers record all of it.
/// </para>
/// </remarks>
public sealed class EditTransactionService(
    ITransactionRepository transactions,
    IPartyRepository parties,
    ILabelRepository labels,
    IAuditRepository audit,
    IClock clock)
{
    /// <summary>The table name the audit trail files transactions under.</summary>
    public const string EntityType = "Transactions";

    /// <summary>One transaction with its parties, label, apps, and tags loaded.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<Transaction?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        transactions.FindAsync(id, cancellationToken);

    /// <summary>Everything that has happened to a transaction, newest first.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IReadOnlyList<AuditEvent>> TimelineAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        audit.TimelineAsync(EntityType, id, cancellationToken);

    /// <summary>
    /// Applies an edit, re-checking every invariant capture checks.
    /// </summary>
    /// <param name="request">What to change it to.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<CaptureResult> EditAsync(
        EditRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var transaction = await transactions.FindAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (transaction is null)
        {
            return CaptureResult.Fail("That transaction no longer exists.");
        }

        // T2: money cannot move to where it already is.
        if (request.SourcePartyId == request.DestinationPartyId)
        {
            return CaptureResult.Fail("Source and destination are the same. Money cannot move to where it already is.");
        }

        // T3: zero is meaningless, negatives are prohibited by M4.
        if (request.AmountMinor <= 0)
        {
            return CaptureResult.Fail("Enter an amount greater than zero.");
        }

        // T7: the same rule as capture. An edit is not a back door to a future date.
        if (request.OccurredOn > clock.Today)
        {
            return CaptureResult.Fail("That date is in the future. Scheduled transactions are not supported yet.");
        }

        var source = await parties.FindAsync(request.SourcePartyId, cancellationToken).ConfigureAwait(false);
        var destination = await parties.FindAsync(request.DestinationPartyId, cancellationToken).ConfigureAwait(false);

        if (source is null || destination is null)
        {
            return CaptureResult.Fail("One of the parties no longer exists. Pick it again.");
        }

        // T1: External to External is not the user's money.
        var kind = TransactionKinds.From(source.Kind, destination.Kind);
        if (kind is null)
        {
            return CaptureResult.Fail(
                "Both ends would be outside your money, so this would stop being a transaction EleFi can hold. "
                + "At least one side must be one of your containers.");
        }

        // Labels are optional and unrestricted (ADR-0013). A Self Transfer drops them,
        // because moving your own money is not a category of spending.
        var labelIds = kind == TransactionKind.SelfTransfer
            ? []
            : (request.LabelIds ?? []).Distinct().ToList();

        if (labelIds.Count > 0)
        {
            var known = (await labels.ListAsync(cancellationToken).ConfigureAwait(false))
                .Select(l => l.Id)
                .ToHashSet();

            if (labelIds.Exists(id => !known.Contains(id)))
            {
                return CaptureResult.Fail("One of those labels no longer exists.");
            }
        }

        transaction.SourcePartyId = request.SourcePartyId;
        transaction.DestinationPartyId = request.DestinationPartyId;
        transaction.SourceAmountMinor = request.AmountMinor;
        transaction.DestinationAmountMinor = request.AmountMinor;
        transaction.OccurredOn = request.OccurredOn;
        transaction.OccurredAtTime = request.OccurredAtTime;
        transaction.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        transaction.MarketplaceAppId = request.MarketplaceAppId;
        transaction.PaymentAppId = request.PaymentAppId;
        transaction.NeedsReview = request.NeedsReview;

        // The editor shows the full set, so what comes back is the complete answer including
        // anything the user unticked. Applied as a difference rather than clear-and-re-add:
        // the join table has audit triggers on insert and delete, so re-adding an unchanged
        // label would write "removed Food" and "added Food" into the timeline on every save.
        var wanted = labelIds.ToHashSet();

        transaction.Labels.RemoveAll(existing => !wanted.Contains(existing.LabelId));

        var already = transaction.Labels.Select(l => l.LabelId).ToHashSet();
        foreach (var labelId in labelIds.Where(id => !already.Contains(id)))
        {
            transaction.Labels.Add(new TransactionLabel { TransactionId = transaction.Id, LabelId = labelId });
        }

        await transactions.UpdateAsync(transaction, cancellationToken).ConfigureAwait(false);

        await parties.TouchAsync([request.SourcePartyId, request.DestinationPartyId], cancellationToken)
            .ConfigureAwait(false);

        return CaptureResult.Ok(transaction);
    }

    /// <summary>
    /// Clears the review flag without changing anything else.
    /// </summary>
    /// <remarks>
    /// The point of the flag: a quick capture guessed, and this is the user saying the guess
    /// was right. Separate from <see cref="EditAsync"/> so confirming costs one tap.
    /// </remarks>
    /// <param name="id">The transaction.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<CaptureResult> MarkReviewedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await transactions.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (transaction is null)
        {
            return CaptureResult.Fail("That transaction no longer exists.");
        }

        transaction.NeedsReview = false;
        await transactions.UpdateAsync(transaction, cancellationToken).ConfigureAwait(false);

        return CaptureResult.Ok(transaction);
    }

    /// <summary>Soft-deletes a transaction. It leaves every balance immediately (T9).</summary>
    /// <param name="id">The transaction.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        transactions.SoftDeleteAsync(id, cancellationToken);

    /// <summary>Deleted transactions, most recently deleted first, for the Deleted screen.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IReadOnlyList<Transaction>> DeletedAsync(CancellationToken cancellationToken = default) =>
        transactions.ListDeletedAsync(cancellationToken);

    /// <summary>
    /// Brings a deleted transaction back into every balance, flagged for review.
    /// </summary>
    /// <remarks>
    /// Flagged because it was deleted for a reason. Coming back unflagged, it would sit in
    /// the list looking settled while the reason it was removed went unexamined. The audit
    /// trail records the restore and the flag.
    /// </remarks>
    /// <param name="id">The transaction.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await transactions.RestoreAsync(id, cancellationToken).ConfigureAwait(false);

        var restored = await transactions.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (restored is not null && !restored.NeedsReview)
        {
            restored.NeedsReview = true;
            await transactions.UpdateAsync(restored, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Restores every deleted transaction, each flagged for review.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>How many were restored.</returns>
    public async Task<int> RestoreAllAsync(CancellationToken cancellationToken = default)
    {
        var deleted = await transactions.ListDeletedAsync(cancellationToken).ConfigureAwait(false);

        foreach (var transaction in deleted)
        {
            await RestoreAsync(transaction.Id, cancellationToken).ConfigureAwait(false);
        }

        return deleted.Count;
    }

    /// <summary>
    /// Turns a stored field name and value into something a person can read.
    /// </summary>
    /// <remarks>
    /// The trail stores column names and raw values because that is what a trigger can see.
    /// Translating happens here rather than in the database so it can change without a
    /// migration.
    /// </remarks>
    /// <param name="field">The column name.</param>
    public static string FieldLabel(string field) => field switch
    {
        "SourcePartyId" => "Paid from",
        "DestinationPartyId" => "Paid to",
        "SourceAmountMinor" or "DestinationAmountMinor" => "Amount",
        "SourceCurrencyCode" or "DestinationCurrencyCode" => "Currency",
        "OccurredOn" => "Date",
        "OccurredAtTime" => "Time",
        "Description" => "Note",
        "MarketplaceAppId" => "Bought through",
        "PaymentAppId" => "Paid with",
        "NeedsReview" => "Needs review",
        "CaptureSource" => "Recorded via",
        "Labels" => "Label",
        _ => field,
    };

    /// <summary>
    /// Formats a changed value, turning minor units back into money.
    /// </summary>
    /// <param name="field">The column the value came from.</param>
    /// <param name="raw">The value as the trail stored it.</param>
    /// <param name="currency">The transaction's currency.</param>
    public static string FieldValue(string field, string raw, Currency currency)
    {
        if (string.Equals(raw, "nothing", StringComparison.Ordinal))
        {
            return raw;
        }

        return field switch
        {
            "SourceAmountMinor" or "DestinationAmountMinor" when long.TryParse(raw, out var minor) =>
                MoneyText.ToDisplayString(Money.SignedMinor(minor, currency)),
            "NeedsReview" => string.Equals(raw, "1", StringComparison.Ordinal) ? "yes" : "no",
            _ => raw,
        };
    }
}
