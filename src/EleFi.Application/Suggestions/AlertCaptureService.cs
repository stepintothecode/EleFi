using EleFi.Application.Abstractions;
using EleFi.Application.Transactions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Suggestions;

/// <summary>What came of one alert.</summary>
/// <param name="Transaction">The transaction recorded or completed, when there was one.</param>
/// <param name="ContainerName">The container it was recorded against, for the prompt.</param>
/// <param name="Reason">Why nothing was recorded, when nothing was.</param>
/// <param name="Merged">True when the alert completed a transaction already recorded from the other channel.</param>
public readonly record struct AlertCaptureResult(
    Transaction? Transaction,
    string? ContainerName,
    string? Reason,
    bool Merged = false)
{
    /// <summary>True when a transaction was recorded or completed.</summary>
    public bool Recorded => Transaction is not null;

    /// <summary>Nothing recorded, and why.</summary>
    /// <param name="reason">What to tell the user.</param>
    public static AlertCaptureResult Nothing(string reason) => new(null, null, reason);
}

/// <summary>
/// Turns Transaction Alerts into transactions flagged Needs Review.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0015 superseded SM2.</b> A parsed bank SMS or Payment App notification is now
/// recorded straight away, as a real transaction flagged Needs Review, the same way a Quick
/// Capture is. Balances include it at once. A wrong one is fixed in the editor or deleted,
/// and a deletion is restorable from Settings.
/// </para>
/// <para>
/// Each alert still produces a <see cref="CaptureSuggestion"/>, recorded already confirmed,
/// as the link between the alerts and the transaction they made. It is what lets the bank's
/// SMS and the app's notification for one payment become one transaction rather than two,
/// in whichever order they arrive, and what suppresses repeats (SM6).
/// </para>
/// <para>
/// The alert text enters an <see cref="SmsBody"/> and goes no further (SM1). Every recorded
/// transaction goes through <see cref="CaptureService"/> or <see cref="EditTransactionService"/>,
/// so T1 to T9 hold unchanged (SM8).
/// </para>
/// </remarks>
public sealed class AlertCaptureService(
    ISuggestionRepository suggestions,
    AlertPartyResolver resolver,
    IAppRepository apps,
    CaptureService capture,
    EditTransactionService editing,
    IClock clock)
{
    /// <summary>How long an alert's link is kept for pairing and duplicate suppression.</summary>
    public static TimeSpan LinkLifetime { get; } = TimeSpan.FromDays(7);

    /// <summary>Offers an alert to the rules and records what it describes.</summary>
    /// <remarks>
    /// Returns "nothing" far more often than not, and that is correct. A rule that matches
    /// the sender but not the body produces nothing rather than a partial guess (FR-11.4).
    /// </remarks>
    /// <param name="channel">Where the alert arrived from.</param>
    /// <param name="sender">The SMS originating address, or the posting app's package.</param>
    /// <param name="messageBody">The alert text. Never stored.</param>
    /// <param name="receivedAt">When the alert arrived.</param>
    /// <param name="homeCurrency">Currency to assume when the alert does not say.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<AlertCaptureResult> IngestAsync(
        AlertChannel channel,
        string sender,
        string messageBody,
        DateTimeOffset receivedAt,
        Currency homeCurrency,
        CancellationToken cancellationToken = default)
    {
        var parsed = await ParseAsync(channel, sender, messageBody, homeCurrency, cancellationToken).ConfigureAwait(false);
        if (parsed is null)
        {
            return AlertCaptureResult.Nothing(
                "EleFi could not read that message. Nothing was recorded, and the text was not kept.");
        }

        var alert = parsed.Value;

        // An app notification is raised as the payment completes, so its time is the
        // payment's. An SMS's is only when the bank noticed, and is not kept.
        if (channel == AlertChannel.PaymentApp)
        {
            alert = alert with { OccurredAtTime = TimeOnly.FromDateTime(receivedAt.ToLocalTime().DateTime) };
        }

        var fingerprint = CaptureSuggestion.ComputeFingerprint(sender, receivedAt, alert.AmountMinor, alert.Last4);

        // SM6: one payment, one transaction, however many times its alert arrives.
        if (await suggestions.ExistsAsync(fingerprint, cancellationToken).ConfigureAwait(false))
        {
            return AlertCaptureResult.Nothing("That payment has already been recorded.");
        }

        await suggestions.PurgeExpiredAsync(clock.UtcNow, cancellationToken).ConfigureAwait(false);

        var containerId = await resolver.MatchLast4Async(alert.Last4, cancellationToken).ConfigureAwait(false);

        var recent = await suggestions
            .ListSinceAsync(receivedAt - AlertCorroboration.Window, cancellationToken)
            .ConfigureAwait(false);

        if (AlertCorroboration.FindPartner(recent, alert, receivedAt) is { } partner)
        {
            partner.Corroborate(alert, fingerprint, containerId);
            await suggestions.UpdateAsync(partner, cancellationToken).ConfigureAwait(false);
            return await CompleteAsync(partner, cancellationToken).ConfigureAwait(false);
        }

        var suggestion = CaptureSuggestion.FromAlert(
            alert, fingerprint, containerId, clock.Today, clock.UtcNow, LinkLifetime);

        return await RecordAsync(suggestion, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// What the rules would read from a message, without recording anything.
    /// </summary>
    /// <remarks>For the Teach screen, which shows the user what EleFi makes of a message first.</remarks>
    /// <param name="channel">Which channel's rules to use.</param>
    /// <param name="sender">The sender.</param>
    /// <param name="messageBody">The message. Never stored.</param>
    /// <param name="homeCurrency">Currency to assume when the message does not say.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ParsedAlert?> ParseAsync(
        AlertChannel channel,
        string sender,
        string messageBody,
        Currency homeCurrency,
        CancellationToken cancellationToken = default)
    {
        var rules = await suggestions.ListRulesAsync(cancellationToken).ConfigureAwait(false);

        var compiled = new List<CompiledParseRule>();
        foreach (var rule in rules)
        {
            if (rule.TryCompile(out var c, out _) && c is not null)
            {
                compiled.Add(c);
            }
        }

        // The body enters an SmsBody here and cannot leave this statement (SM1).
        return AlertParser.Parse(channel, sender, new SmsBody(messageBody), compiled, homeCurrency);
    }

    private async Task<AlertCaptureResult> RecordAsync(CaptureSuggestion suggestion, CancellationToken cancellationToken)
    {
        var ends = await resolver.ResolveAsync(suggestion, cancellationToken).ConfigureAwait(false);
        if (ends is null)
        {
            return AlertCaptureResult.Nothing("Add a container first, so a payment has somewhere to be recorded.");
        }

        var result = await capture.CaptureAsync(
            new CaptureRequest(
                ends.SourcePartyId,
                ends.DestinationPartyId,
                suggestion.AmountMinor,
                suggestion.CurrencyCode,
                suggestion.AmountMinor,
                suggestion.CurrencyCode,
                suggestion.OccurredOn ?? clock.Today,
                suggestion.OccurredAtTime,
                LabelIds: null,
                Description: suggestion.Note,
                PaymentAppId: await PaymentAppIdAsync(suggestion, cancellationToken).ConfigureAwait(false),
                NeedsReview: true,
                CaptureSource: SourceOf(suggestion)),
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return AlertCaptureResult.Nothing(result.Error!);
        }

        suggestion.State = SuggestionState.Confirmed;
        suggestion.TransactionId = result.Transaction!.Id;
        await suggestions.AddAsync(suggestion, cancellationToken).ConfigureAwait(false);

        return new AlertCaptureResult(result.Transaction, ends.Container.Name, null);
    }

    // The second alert for a payment completes the transaction the first one recorded: the
    // bank's card, the app's payee and note. Only while it still needs review: once the user
    // has checked it, their answer stands and the late alert changes nothing.
    private async Task<AlertCaptureResult> CompleteAsync(CaptureSuggestion merged, CancellationToken cancellationToken)
    {
        var existing = merged.TransactionId is { } id
            ? await editing.FindAsync(id, cancellationToken).ConfigureAwait(false)
            : null;

        if (existing is null)
        {
            return AlertCaptureResult.Nothing("That payment was recorded and then deleted, so it was left alone.");
        }

        if (!existing.NeedsReview)
        {
            return AlertCaptureResult.Nothing("That payment is already recorded and reviewed.");
        }

        var ends = await resolver.ResolveAsync(merged, cancellationToken).ConfigureAwait(false);
        if (ends is null)
        {
            return AlertCaptureResult.Nothing("That payment is already recorded.");
        }

        var result = await editing.EditAsync(
            new EditRequest(
                existing.Id,
                ends.SourcePartyId,
                ends.DestinationPartyId,
                existing.SourceAmountMinor,
                merged.OccurredOn ?? existing.OccurredOn,
                existing.OccurredAtTime ?? merged.OccurredAtTime,
                [.. existing.Labels.Select(l => l.LabelId)],
                existing.Description ?? merged.Note,
                existing.MarketplaceAppId,
                existing.PaymentAppId ?? await PaymentAppIdAsync(merged, cancellationToken).ConfigureAwait(false),
                NeedsReview: true),
            cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? new AlertCaptureResult(result.Transaction, ends.Container.Name, null, Merged: true)
            : AlertCaptureResult.Nothing(result.Error!);
    }

    private async Task<Guid?> PaymentAppIdAsync(CaptureSuggestion suggestion, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(suggestion.PaymentAppName)
            ? null
            : (await apps.GetOrCreateAsync(suggestion.PaymentAppName, cancellationToken).ConfigureAwait(false)).Id;

    // The bank's message names the account, so a payment both channels described is
    // recorded as coming from the SMS.
    private static CaptureSource SourceOf(CaptureSuggestion suggestion) =>
        suggestion.Evidence.HasFlag(AlertEvidence.Sms) ? CaptureSource.Sms : CaptureSource.PaymentApp;
}
