using EleFi.Application.Abstractions;
using EleFi.Domain.Labels;
using EleFi.Domain.Money;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Transactions;

/// <summary>What the capture form gathered, before anything is validated or written.</summary>
/// <param name="SourcePartyId">The party money left.</param>
/// <param name="DestinationPartyId">The party money reached.</param>
/// <param name="SourceAmountMinor">What left, in minor units.</param>
/// <param name="SourceCurrencyCode">The source currency.</param>
/// <param name="DestinationAmountMinor">What arrived, in minor units. Equals the source amount when currencies match.</param>
/// <param name="DestinationCurrencyCode">The destination currency.</param>
/// <param name="OccurredOn">The calendar day it happened.</param>
/// <param name="OccurredAtTime">The time of day, or null when only the date is known.</param>
/// <param name="LabelIds">Categories. Any number, including none.</param>
/// <param name="Description">Optional free text.</param>
/// <param name="MarketplaceAppId">Optional platform bought through.</param>
/// <param name="PaymentAppId">Optional rail money moved along.</param>
/// <param name="NeedsReview">Whether the values were guessed.</param>
/// <param name="CaptureSource">How this was recorded.</param>
public sealed record CaptureRequest(
    Guid SourcePartyId,
    Guid DestinationPartyId,
    long SourceAmountMinor,
    string SourceCurrencyCode,
    long DestinationAmountMinor,
    string DestinationCurrencyCode,
    DateOnly OccurredOn,
    TimeOnly? OccurredAtTime = null,
    IReadOnlyList<Guid>? LabelIds = null,
    string? Description = null,
    Guid? MarketplaceAppId = null,
    Guid? PaymentAppId = null,
    bool NeedsReview = false,
    CaptureSource CaptureSource = CaptureSource.App);

/// <summary>The outcome of a capture attempt.</summary>
/// <param name="Transaction">The saved transaction, when it succeeded.</param>
/// <param name="Error">Why it was refused, when it failed.</param>
public readonly record struct CaptureResult(Transaction? Transaction, string? Error)
{
    /// <summary>True when a transaction was written.</summary>
    public bool Succeeded => Transaction is not null;

    /// <summary>A successful capture.</summary>
    /// <param name="transaction">The saved transaction.</param>
    public static CaptureResult Ok(Transaction transaction) => new(transaction, null);

    /// <summary>A refused capture, with a reason to show the user.</summary>
    /// <param name="error">What was wrong, in words the user can act on.</param>
    public static CaptureResult Fail(string error) => new(null, error);
}

/// <summary>
/// Records transactions. The one path money enters the ledger by.
/// </summary>
/// <remarks>
/// Every route into the app comes through here: the capture form, a confirmed Capture
/// Suggestion, and later the widget and the notification. There is no privileged write
/// (SM8), so invariants T1 to T9 are checked once rather than once per surface.
/// </remarks>
public sealed class CaptureService(
    ITransactionRepository transactions,
    IPartyRepository parties,
    IAppRepository apps,
    ILabelRepository labels,
    IClock clock)
{
    /// <summary>
    /// Validates and records a transaction.
    /// </summary>
    /// <remarks>
    /// Returns a reason rather than throwing for anything the user could have typed. An
    /// exception is for a bug; a future date is a mistake, and mistakes deserve a sentence
    /// that says what to do.
    /// </remarks>
    /// <param name="request">What the form gathered.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<CaptureResult> CaptureAsync(
        CaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // T2: money cannot move to where it already is.
        if (request.SourcePartyId == request.DestinationPartyId)
        {
            return CaptureResult.Fail("Source and destination are the same. Money cannot move to where it already is.");
        }

        // T3: zero is meaningless, and negatives are prohibited by M4.
        if (request.SourceAmountMinor <= 0 || request.DestinationAmountMinor <= 0)
        {
            return CaptureResult.Fail("Enter an amount greater than zero.");
        }

        if (!Currency.TryOf(request.SourceCurrencyCode, out var sourceCurrency)
            || !Currency.TryOf(request.DestinationCurrencyCode, out var destinationCurrency))
        {
            return CaptureResult.Fail("That currency code is not one I recognise.");
        }

        // T4: same currency means the two ends must agree, so a transaction cannot lose or
        // invent money. Enforced here and again by a CHECK constraint in the database.
        var sameCurrency = string.Equals(sourceCurrency.Code, destinationCurrency.Code, StringComparison.Ordinal);
        if (sameCurrency && request.SourceAmountMinor != request.DestinationAmountMinor)
        {
            return CaptureResult.Fail("Both ends are in the same currency, so the two amounts must match.");
        }

        // T7: future dates are rejected in v1. Letting them into balances silently would
        // make today's net worth wrong, and scheduling is a feature, not a side effect.
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

        // T1: External to External is not the user's money and recording it would corrupt
        // every report that follows.
        var kind = TransactionKinds.From(source.Kind, destination.Kind);
        if (kind is null)
        {
            return CaptureResult.Fail(
                "Both ends are outside your money, so this is not a transaction EleFi can record. "
                + "At least one side must be one of your containers.");
        }

        // Labels are optional and unrestricted (ADR-0013). A Self Transfer still carries
        // none by default, because moving your own money is not a category of spending, but
        // nothing stops one being added.
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

        var now = clock.UtcNow;
        var transaction = new Transaction
        {
            SourcePartyId = request.SourcePartyId,
            DestinationPartyId = request.DestinationPartyId,
            SourceAmountMinor = request.SourceAmountMinor,
            SourceCurrencyCode = sourceCurrency.Code,
            DestinationAmountMinor = request.DestinationAmountMinor,
            DestinationCurrencyCode = destinationCurrency.Code,
            OccurredOn = request.OccurredOn,
            OccurredAtTime = request.OccurredAtTime,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            MarketplaceAppId = request.MarketplaceAppId,
            PaymentAppId = request.PaymentAppId,
            NeedsReview = request.NeedsReview,
            CaptureSource = request.CaptureSource,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var labelId in labelIds)
        {
            transaction.Labels.Add(new TransactionLabel { TransactionId = transaction.Id, LabelId = labelId });
        }

        await transactions.AddAsync(transaction, cancellationToken).ConfigureAwait(false);

        // Suggestion ranking is a side effect of use, not something the user maintains.
        await parties.TouchAsync([request.SourcePartyId, request.DestinationPartyId], cancellationToken)
            .ConfigureAwait(false);

        var usedApps = new[] { request.MarketplaceAppId, request.PaymentAppId }
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToList();

        if (usedApps.Count > 0)
        {
            await apps.TouchAsync(usedApps, cancellationToken).ConfigureAwait(false);
        }

        return CaptureResult.Ok(transaction);
    }
}
