using EleFi.Application.Abstractions;
using EleFi.Application.Transactions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Suggestions;

/// <summary>The outcome of offering an alert to the parser.</summary>
/// <param name="Suggestion">The suggestion created or completed, when there was one.</param>
/// <param name="Reason">Why nothing was created, when nothing was.</param>
/// <param name="Corroborated">True when the alert completed an existing suggestion rather than creating one.</param>
public readonly record struct IngestResult(CaptureSuggestion? Suggestion, string? Reason, bool Corroborated = false)
{
    /// <summary>True when a suggestion was created or completed.</summary>
    public bool Created => Suggestion is not null;
}

/// <summary>
/// Turns Transaction Alerts into Capture Suggestions, and confirmed suggestions into
/// transactions.
/// </summary>
/// <remarks>
/// <para>
/// <b>SM2 governs everything here.</b> Nothing in this class writes a transaction on its
/// own. <see cref="IngestAsync(AlertChannel, string, string, DateTimeOffset, Currency, CancellationToken)"/>
/// produces or completes a suggestion and stops; only <see cref="ConfirmAsync"/> creates
/// money, and it is called from a button a human pressed.
/// </para>
/// <para>
/// The message body arrives as text and goes straight into an <see cref="SmsBody"/>, which
/// cannot be stored, logged, or captured (SM1). There is no field on any type here that
/// could hold it, and the same rule covers a Payment App notification's text (ADR-0014).
/// </para>
/// </remarks>
public sealed class SuggestionService(
    ISuggestionRepository suggestions,
    IContainerRepository containers,
    IPartyRepository parties,
    IAppRepository apps,
    CaptureService capture,
    IClock clock)
{
    /// <summary>How long a suggestion is offered before it is destroyed (FR-11.12).</summary>
    public static TimeSpan DefaultLifetime { get; } = TimeSpan.FromDays(7);

    /// <summary>Offers an SMS to the enabled rules and stores a suggestion if one matches.</summary>
    /// <param name="sender">The SMS originating address.</param>
    /// <param name="messageBody">The message text. Never stored.</param>
    /// <param name="receivedAt">When the message arrived.</param>
    /// <param name="homeCurrency">Currency to assume when the message does not say.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IngestResult> IngestAsync(
        string sender,
        string messageBody,
        DateTimeOffset receivedAt,
        Currency homeCurrency,
        CancellationToken cancellationToken = default) =>
        IngestAsync(AlertChannel.Sms, sender, messageBody, receivedAt, homeCurrency, cancellationToken);

    /// <summary>
    /// Offers an alert to the enabled rules for its channel, and stores or completes a
    /// suggestion if one matches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns "no match" far more often than not, and that is correct. A rule that matches
    /// the sender but not the body produces nothing rather than a partial guess (FR-11.4).
    /// </para>
    /// <para>
    /// When the same payment is already waiting as a suggestion from the other channel, the
    /// alert completes that one instead of creating a second (see
    /// <see cref="AlertCorroboration"/>): the bank's SMS supplies the card, the Payment App
    /// supplies the payee's name and the note.
    /// </para>
    /// </remarks>
    /// <param name="channel">Where the alert arrived from.</param>
    /// <param name="sender">The SMS originating address, or the posting app's package.</param>
    /// <param name="messageBody">The alert text. Never stored.</param>
    /// <param name="receivedAt">When the alert arrived.</param>
    /// <param name="homeCurrency">Currency to assume when the alert does not say.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IngestResult> IngestAsync(
        AlertChannel channel,
        string sender,
        string messageBody,
        DateTimeOffset receivedAt,
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
        var parsed = AlertParser.Parse(channel, sender, new SmsBody(messageBody), compiled, homeCurrency);
        if (parsed is null)
        {
            return new IngestResult(
                null,
                "No enabled rule recognised that message. Nothing was stored, and the text was not kept.");
        }

        var alert = parsed.Value;

        // An app notification is raised as the payment completes, so its time is the
        // payment's. An SMS's is only when the bank noticed, and is not kept.
        if (channel == AlertChannel.PaymentApp)
        {
            alert = alert with { OccurredAtTime = TimeOnly.FromDateTime(receivedAt.ToLocalTime().DateTime) };
        }

        var fingerprint = CaptureSuggestion.ComputeFingerprint(sender, receivedAt, alert.AmountMinor, alert.Last4);

        // SM6: one real payment produces one suggestion, however many times its alert
        // arrives, including a notification the app reposts after it was merged.
        if (await suggestions.ExistsAsync(fingerprint, cancellationToken).ConfigureAwait(false))
        {
            return new IngestResult(null, "That looks like a message you have already been offered.");
        }

        var containerId = await MatchContainerAsync(alert.Last4, cancellationToken).ConfigureAwait(false);

        var recent = await suggestions
            .ListSinceAsync(receivedAt - AlertCorroboration.Window, cancellationToken)
            .ConfigureAwait(false);

        if (AlertCorroboration.FindPartner(recent, alert, receivedAt) is { } partner)
        {
            partner.Corroborate(alert, fingerprint, containerId);
            await suggestions.UpdateAsync(partner, cancellationToken).ConfigureAwait(false);

            // Already added from the other channel's alert. Remembered, so a repost is a
            // duplicate too, but not offered: that would invite recording it twice.
            return partner.IsPending
                ? new IngestResult(partner, null, Corroborated: true)
                : new IngestResult(null, "That payment has already been added.", Corroborated: true);
        }

        var suggestion = CaptureSuggestion.FromAlert(
            alert, fingerprint, containerId, clock.Today, clock.UtcNow, DefaultLifetime);

        await suggestions.AddAsync(suggestion, cancellationToken).ConfigureAwait(false);
        return new IngestResult(suggestion, null);
    }

    /// <summary>Suggestions still waiting for the user.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IReadOnlyList<CaptureSuggestion>> PendingAsync(CancellationToken cancellationToken = default) =>
        suggestions.ListPendingAsync(cancellationToken);

    /// <summary>How many suggestions are waiting, for the dashboard.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<int> PendingCountAsync(CancellationToken cancellationToken = default) =>
        suggestions.CountPendingAsync(cancellationToken);

    /// <summary>One suggestion, or null.</summary>
    /// <param name="suggestionId">The suggestion.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<CaptureSuggestion?> FindAsync(Guid suggestionId, CancellationToken cancellationToken = default) =>
        suggestions.FindAsync(suggestionId, cancellationToken);

    /// <summary>
    /// Turns a suggestion into a real transaction, once the user has said yes.
    /// </summary>
    /// <remarks>
    /// Goes through <see cref="CaptureService"/> like every other route, so T1 to T9 apply
    /// unchanged (SM8). There is no privileged write for machine-derived data. The Payment
    /// App the alert named is recorded as the transaction's Paid with, and its note as the
    /// transaction's note.
    /// </remarks>
    /// <param name="suggestionId">The suggestion.</param>
    /// <param name="containerId">The container to use, overriding any matched one.</param>
    /// <param name="counterpartyName">
    /// The external party name the user settled on. Ignored for a card bill, whose other end
    /// is one of the user's own containers.
    /// </param>
    /// <param name="labelIds">The labels the user chose, if any. Null or empty is normal.</param>
    /// <param name="destinationContainerId">
    /// For a card bill (a Self Transfer suggestion), the card that was paid. Required then,
    /// ignored otherwise.
    /// </param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<CaptureResult> ConfirmAsync(
        Guid suggestionId,
        Guid containerId,
        string counterpartyName,
        IReadOnlyList<Guid>? labelIds = null,
        Guid? destinationContainerId = null,
        CancellationToken cancellationToken = default)
    {
        var suggestion = await suggestions.FindAsync(suggestionId, cancellationToken).ConfigureAwait(false);
        if (suggestion is null)
        {
            return CaptureResult.Fail("That suggestion is gone.");
        }

        // SM7: a confirmed suggestion cannot be confirmed twice. Its transaction id is the
        // proof, and it is what stops a re-parse creating a second entry for one payment.
        if (suggestion.State == SuggestionState.Confirmed)
        {
            return CaptureResult.Fail("That suggestion has already been recorded.");
        }

        var containerParty = await containers.PartyForAsync(containerId, cancellationToken).ConfigureAwait(false);
        if (containerParty is null)
        {
            return CaptureResult.Fail("That container no longer exists.");
        }

        var ends = await ResolveEndsAsync(suggestion, containerParty.Id, counterpartyName, destinationContainerId, cancellationToken)
            .ConfigureAwait(false);
        if (ends.Error is not null)
        {
            return CaptureResult.Fail(ends.Error);
        }

        var (sourceId, destinationId, name) = (ends.SourceId, ends.DestinationId, ends.CounterpartyName);

        Guid? paymentAppId = string.IsNullOrWhiteSpace(suggestion.PaymentAppName)
            ? null
            : (await apps.GetOrCreateAsync(suggestion.PaymentAppName, cancellationToken).ConfigureAwait(false)).Id;

        var result = await capture.CaptureAsync(
            new CaptureRequest(
                sourceId,
                destinationId,
                suggestion.AmountMinor,
                suggestion.CurrencyCode,
                suggestion.AmountMinor,
                suggestion.CurrencyCode,
                suggestion.OccurredOn ?? clock.Today,
                // The app's time when an app reported it. Never the SMS's: that is when the
                // bank noticed, not when the money moved, and inventing a time would be a
                // fact the user never supplied and cannot correct from memory.
                OccurredAtTime: suggestion.OccurredAtTime,
                LabelIds: labelIds,
                Description: DescriptionFor(suggestion, name),
                PaymentAppId: paymentAppId,
                CaptureSource: suggestion.Evidence.HasFlag(AlertEvidence.Sms) ? CaptureSource.Sms : CaptureSource.PaymentApp),
            cancellationToken).ConfigureAwait(false);

        if (result.Succeeded && result.Transaction is not null)
        {
            await suggestions.MarkConfirmedAsync(suggestionId, result.Transaction.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Destroys a suggestion the user rejected.
    /// </summary>
    /// <remarks>
    /// A hard delete (SM5). Soft delete protects user-entered data; this is machine output
    /// the user said no to, and keeping it would mean retaining message-derived content
    /// after an explicit refusal.
    /// </remarks>
    /// <param name="suggestionId">The suggestion.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task DismissAsync(Guid suggestionId, CancellationToken cancellationToken = default) =>
        suggestions.DeleteAsync(suggestionId, cancellationToken);

    /// <summary>Destroys suggestions nobody acted on before they expired.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default) =>
        suggestions.PurgeExpiredAsync(clock.UtcNow, cancellationToken);

    // Which party is at which end. A card bill is a Self Transfer between two of the user's
    // own containers (D1: it is not spending); everything else is one container and one
    // named outsider, with the order set by the direction.
    private async Task<(Guid SourceId, Guid DestinationId, string? CounterpartyName, string? Error)> ResolveEndsAsync(
        CaptureSuggestion suggestion,
        Guid containerPartyId,
        string counterpartyName,
        Guid? destinationContainerId,
        CancellationToken cancellationToken)
    {
        if (suggestion.Direction == TransactionKind.SelfTransfer)
        {
            var card = destinationContainerId is { } id
                ? await containers.PartyForAsync(id, cancellationToken).ConfigureAwait(false)
                : null;

            return card is null
                ? (default, default, null, "Choose which card this paid.")
                : (containerPartyId, card.Id, null, null);
        }

        // No "Unknown" party. A transaction needs a real other end, and a placeholder would
        // become a party in every suggestion list from then on.
        if (string.IsNullOrWhiteSpace(counterpartyName))
        {
            return (default, default, null,
                suggestion.Direction == TransactionKind.Debit ? "Who was it paid to?" : "Who was it received from?");
        }

        var name = counterpartyName.Trim();
        var counterparty = await parties.GetOrCreateExternalAsync(name, cancellationToken).ConfigureAwait(false);

        return suggestion.Direction == TransactionKind.Debit
            ? (containerPartyId, counterparty.Id, name, null)
            : (counterparty.Id, containerPartyId, name, null);
    }

    // SM11: an exact last-four match against exactly one container, or nothing. Never
    // inferred from the counterparty, the amount, or history.
    private async Task<Guid?> MatchContainerAsync(string? last4, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(last4))
        {
            return null;
        }

        var matches = await containers.FindByLast4Async(last4, cancellationToken).ConfigureAwait(false);
        return matches.Count == 1 ? matches[0].Id : null;
    }

    // The note the payer wrote, when there was one. Otherwise the alert's own wording for the
    // payee, kept only when it says something the chosen name does not: "VPA zomato@hdfcbank"
    // beside "Zomato" is worth keeping, "Zomato" beside "Zomato" is noise.
    private static string? DescriptionFor(CaptureSuggestion suggestion, string? chosenName)
    {
        if (!string.IsNullOrWhiteSpace(suggestion.Note))
        {
            return suggestion.Note;
        }

        return string.Equals(suggestion.CounterpartyText?.Trim(), chosenName, StringComparison.OrdinalIgnoreCase)
            ? null
            : suggestion.CounterpartyText;
    }
}
