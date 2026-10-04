using EleFi.Application.Abstractions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Money;

namespace EleFi.Application.Suggestions;

/// <summary>
/// What happens when an SMS or a Payment App notification arrives: check it is wanted, parse
/// it, and raise or update the prompt.
/// </summary>
/// <remarks>
/// <para>
/// The single entry point for both platform receivers, so the gates are written once: the
/// in-app switch for the channel, the Payment App allow-list, and then the parser's own
/// sender and OTP gates. A receiver hands over the text and forgets it.
/// </para>
/// <para>
/// Never records money (SM2). The furthest this goes is a suggestion and a notification
/// asking the user whether it is right.
/// </para>
/// </remarks>
public sealed class IncomingAlertHandler(
    SuggestionService suggestions,
    AlertCaptureSettings settings,
    IContainerRepository containers,
    ISuggestionPromptSurface prompts)
{
    /// <summary>Handles one incoming alert.</summary>
    /// <param name="channel">Where it arrived from.</param>
    /// <param name="sender">The SMS originating address, or the posting app's package.</param>
    /// <param name="text">The alert text. Never stored, never logged.</param>
    /// <param name="receivedAt">When it arrived.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>What came of it.</returns>
    public async Task<IngestResult> HandleAsync(
        AlertChannel channel,
        string sender,
        string text,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        // The in-app switch beats the permission (FR-11.24): off means not read, even
        // though the system would still deliver it.
        if (!settings.IsEnabled(channel))
        {
            return new IngestResult(null, "Reading this kind of alert is switched off.");
        }

        // The receiver already checks this before touching a notification's text. Checked
        // again here so no future caller can skip it.
        if (channel == AlertChannel.PaymentApp && !PaymentApps.IsAllowListed(sender))
        {
            return new IngestResult(null, "That app is not one EleFi reads.");
        }

        var result = await suggestions
            .IngestAsync(channel, sender, text, receivedAt, Currency.Inr, cancellationToken)
            .ConfigureAwait(false);

        if (result.Suggestion is { } suggestion)
        {
            var container = suggestion.ContainerId is { } id
                ? await containers.FindAsync(id, cancellationToken).ConfigureAwait(false)
                : null;

            // Same identity as before when this completed an existing suggestion, so the
            // prompt already on screen is updated in place rather than joined by a second.
            prompts.Show(SuggestionPromptText.For(suggestion, container?.Name));
        }

        return result;
    }

    /// <summary>Dismisses a suggestion from its prompt, and takes the prompt away.</summary>
    /// <param name="suggestionId">The suggestion.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task DismissFromPromptAsync(Guid suggestionId, CancellationToken cancellationToken = default)
    {
        await suggestions.DismissAsync(suggestionId, cancellationToken).ConfigureAwait(false);
        prompts.Withdraw(suggestionId);
    }
}
