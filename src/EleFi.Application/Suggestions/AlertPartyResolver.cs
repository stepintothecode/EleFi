using EleFi.Application.Abstractions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Containers;
using EleFi.Domain.Filters;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Suggestions;

/// <summary>The two ends a recorded alert's transaction gets, and the container among them.</summary>
/// <param name="SourcePartyId">Where the money left.</param>
/// <param name="DestinationPartyId">Where the money arrived.</param>
/// <param name="Container">The user's container at the end the alert was about.</param>
public sealed record AlertEnds(Guid SourcePartyId, Guid DestinationPartyId, Container Container);

/// <summary>
/// Decides which container and which payee an alert's transaction is recorded against.
/// </summary>
/// <remarks>
/// <para>
/// Recording straight away (ADR-0015) means there is always an answer, even when the alert
/// does not name the account. In order: the account the bank's last four digits matched
/// (certain); the Payment App's default container; the container last paid from with that
/// app; the first container in picker order. Anything past the first is a guess, which is
/// what the transaction's Needs Review flag says.
/// </para>
/// <para>
/// A credit-card bill is recorded as a Self Transfer to a card (D1), never as spending. The
/// card is the one the alert's wording names, or the only card there is.
/// </para>
/// </remarks>
public sealed class AlertPartyResolver(
    IContainerRepository containers,
    IPartyRepository parties,
    IAppRepository apps,
    ITransactionRepository transactions)
{
    /// <summary>The name used when an alert names nobody.</summary>
    public const string UnknownPayee = "Unknown payee";

    /// <summary>
    /// The container an alert's last four digits name, when exactly one does (SM11).
    /// </summary>
    /// <remarks>
    /// The only answer treated as certain. Two cards ending 4417 is a real situation, and
    /// the right response is no match rather than a coin toss.
    /// </remarks>
    /// <param name="last4">The digits the alert carried, if any.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<Guid?> MatchLast4Async(string? last4, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(last4))
        {
            return null;
        }

        var matches = await containers.FindByLast4Async(last4, cancellationToken).ConfigureAwait(false);
        return matches.Count == 1 ? matches[0].Id : null;
    }

    /// <summary>Works out both ends, or null when there is no container to record against.</summary>
    /// <param name="suggestion">What the alerts said.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<AlertEnds?> ResolveAsync(CaptureSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        var selectable = ContainerGrouping.InPickerOrder(
            await containers.ListSelectableAsync(cancellationToken).ConfigureAwait(false));

        var container = await ContainerForAsync(suggestion, selectable, cancellationToken).ConfigureAwait(false);
        if (container is null)
        {
            return null;
        }

        var containerParty = await containers.PartyForAsync(container.Id, cancellationToken).ConfigureAwait(false);
        if (containerParty is null)
        {
            return null;
        }

        if (suggestion.Direction == TransactionKind.SelfTransfer
            && CardFor(suggestion.CounterpartyText, selectable, container) is { } card
            && await containers.PartyForAsync(card.Id, cancellationToken).ConfigureAwait(false) is { } cardParty)
        {
            return new AlertEnds(containerParty.Id, cardParty.Id, container);
        }

        var name = string.IsNullOrWhiteSpace(suggestion.CounterpartyText)
            ? suggestion.PaymentAppName ?? UnknownPayee
            : suggestion.CounterpartyText;

        var external = await parties.GetOrCreateExternalAsync(name, cancellationToken).ConfigureAwait(false);

        // A card bill with no card to land on can only be recorded as money leaving.
        return suggestion.Direction == TransactionKind.Credit
            ? new AlertEnds(external.Id, containerParty.Id, container)
            : new AlertEnds(containerParty.Id, external.Id, container);
    }

    /// <summary>Picks the card a card-bill alert paid.</summary>
    /// <param name="wording">What the alert called it: "HDFC Credit Card".</param>
    /// <param name="selectable">The user's containers, in picker order.</param>
    /// <param name="payingFrom">The container the bill was paid from, never the answer.</param>
    public static Container? CardFor(string? wording, IReadOnlyList<Container> selectable, Container payingFrom)
    {
        ArgumentNullException.ThrowIfNull(selectable);
        ArgumentNullException.ThrowIfNull(payingFrom);

        var cards = selectable
            .Where(c => c.Kind == ContainerKind.CreditCard && c.Id != payingFrom.Id)
            .ToList();

        if (cards.Count <= 1 || string.IsNullOrWhiteSpace(wording))
        {
            return cards.FirstOrDefault();
        }

        // The card whose name or bank appears in the wording. One clear match wins; anything
        // else falls back to the first card, and the review flag carries the doubt.
        var named = cards
            .Where(c => Mentions(wording, c.Name) || Mentions(wording, c.InstitutionName))
            .ToList();

        return named.Count == 1 ? named[0] : cards[0];
    }

    private async Task<Container?> ContainerForAsync(
        CaptureSuggestion suggestion,
        IReadOnlyList<Container> selectable,
        CancellationToken cancellationToken)
    {
        // SM11 still holds for the certain answer: only an exact last-four match is treated
        // as known. Everything after it is a guess.
        if (suggestion.ContainerId is { } matched && selectable.FirstOrDefault(c => c.Id == matched) is { } known)
        {
            return known;
        }

        if (!string.IsNullOrWhiteSpace(suggestion.PaymentAppName))
        {
            var app = await apps.GetOrCreateAsync(suggestion.PaymentAppName, cancellationToken).ConfigureAwait(false);

            if (app.DefaultContainerId is { } preferred && selectable.FirstOrDefault(c => c.Id == preferred) is { } chosen)
            {
                return chosen;
            }

            var lastWithApp = await transactions
                .QueryAsync(new TransactionFilter { AppIds = [app.Id] }, 0, 1, cancellationToken)
                .ConfigureAwait(false);

            var usedId = lastWithApp.Count == 0
                ? null
                : lastWithApp[0].SourceParty?.ContainerId ?? lastWithApp[0].DestinationParty?.ContainerId;

            if (usedId is { } id && selectable.FirstOrDefault(c => c.Id == id) is { } used)
            {
                return used;
            }
        }

        // A card bill is paid from a bank or a wallet, never from another card, so the last
        // resort for one skips the cards that otherwise lead the picker order.
        return suggestion.Direction == TransactionKind.SelfTransfer
            ? selectable.FirstOrDefault(c => c.Kind != ContainerKind.CreditCard) ?? (selectable.Count == 0 ? null : selectable[0])
            : (selectable.Count == 0 ? null : selectable[0]);
    }

    // Matches the first word of a name, so "HDFC Regalia" is found in "HDFC Credit Card".
    private static bool Mentions(string wording, string? name)
    {
        var first = name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first is { Length: >= 3 } && wording.Contains(first, StringComparison.OrdinalIgnoreCase);
    }
}
