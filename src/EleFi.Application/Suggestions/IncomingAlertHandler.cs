using EleFi.Application.Transactions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Money;
using EleFi.Domain.Notices;

namespace EleFi.Application.Suggestions;

/// <summary>
/// What happens when an SMS or a Payment App notification arrives: check it is wanted,
/// record what it describes, and say so.
/// </summary>
/// <remarks>
/// The single entry point for both platform receivers, so the gates are written once: the
/// in-app switch for the channel, the Payment App allow-list, and then the parser's own
/// sender and OTP gates. A receiver hands over the text and forgets it.
/// </remarks>
public sealed class IncomingAlertHandler(
    AlertCaptureService capture,
    AlertCaptureSettings settings,
    EditTransactionService editing,
    Planning.PlanService planner,
    Abstractions.INoticeRepository notices,
    Abstractions.IAlertPromptSurface prompts,
    Abstractions.IClock clock)
{
    /// <summary>Handles one incoming alert.</summary>
    /// <param name="channel">Where it arrived from.</param>
    /// <param name="sender">The SMS originating address, or the posting app's package.</param>
    /// <param name="text">The alert text. Never stored, never logged.</param>
    /// <param name="receivedAt">When it arrived.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>What came of it.</returns>
    public async Task<AlertCaptureResult> HandleAsync(
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
            return AlertCaptureResult.Nothing("Reading this kind of alert is switched off.");
        }

        // The receiver already checks this before touching a notification's text. Checked
        // again here so no future caller can skip it.
        if (channel == AlertChannel.PaymentApp && !PaymentApps.IsAllowListed(sender))
        {
            return AlertCaptureResult.Nothing("That app is not one EleFi reads.");
        }

        var result = await capture
            .IngestAsync(channel, sender, text, receivedAt, Currency.Inr, cancellationToken)
            .ConfigureAwait(false);

        if (result.Transaction is { } transaction)
        {
            // Same identity when this completed an earlier alert's transaction, so the
            // prompt already on screen is updated in place rather than joined by a second.
            var app = transaction.PaymentApp?.Name ?? PaymentApps.Find(sender)?.Name;

            // The SIP whose SMS came ticks itself off; the one whose SMS never came stays open.
            var plan = await planner.CompleteMatchingAsync(transaction, cancellationToken).ConfigureAwait(false);
            var prompt = AlertPromptText.For(transaction, result.ContainerName, app, plan?.Title);
            prompts.Show(prompt);

            // Kept in the app's own list too, so it can be read in full after the system
            // notification has been swiped away. One entry per transaction: a merged second
            // alert rewrites it with the fuller story rather than adding another.
            await notices.UpsertAsync(
                new Notice
                {
                    Title = prompt.Title,
                    Body = prompt.Body,
                    Route = $"transactions/{transaction.Id}",
                    CreatedAt = clock.UtcNow,
                    UpdatedAt = clock.UtcNow,
                },
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Deletes a recorded transaction from its prompt, for an alert that was not really a
    /// payment, and takes the prompt away.
    /// </summary>
    /// <remarks>A soft delete: it can be restored from Settings, flagged for review.</remarks>
    /// <param name="transactionId">The transaction.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task DeleteFromPromptAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        await editing.DeleteAsync(transactionId, cancellationToken).ConfigureAwait(false);
        prompts.Withdraw(transactionId);
    }
}
