namespace EleFi.Domain.Alerts;

/// <summary>
/// Decides whether a new alert describes a payment a pending suggestion already holds.
/// </summary>
/// <remarks>
/// <para>
/// One UPI payment usually produces two alerts within seconds: the Payment App's "Paid ₹450
/// to Zomato" and the bank's "Rs.450.00 debited from a/c XX4417". Recording both would
/// count the same money twice.
/// </para>
/// <para>
/// The match is deliberately strict: the same amount to the paisa, the same currency, the
/// same movement (a card bill's Self Transfer pairs with the bank's Debit), from the other
/// channel, within <see cref="Window"/>. Two genuinely
/// separate ₹20 chais from the same channel never merge, because a suggestion takes at most
/// one alert from each channel.
/// </para>
/// </remarks>
public static class AlertCorroboration
{
    /// <summary>How far apart two alerts about one payment can arrive.</summary>
    /// <remarks>
    /// Bank SMS are sometimes held up by the operator for minutes. Fifteen covers a slow
    /// delivery without pairing payments made at lunch with ones made at dinner.
    /// </remarks>
    public static TimeSpan Window { get; } = TimeSpan.FromMinutes(15);

    /// <summary>True when the alert could be the other half of this suggestion.</summary>
    /// <remarks>
    /// A confirmed suggestion still matches. The bank's SMS for a payment the user already
    /// added from the app's alert is the same payment, and offering it again would invite
    /// recording it twice. The caller decides what a confirmed partner means.
    /// </remarks>
    /// <param name="suggestion">A suggestion.</param>
    /// <param name="alert">The new alert.</param>
    /// <param name="receivedAt">When the new alert arrived.</param>
    public static bool Matches(CaptureSuggestion suggestion, ParsedAlert alert, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        return suggestion.State is SuggestionState.Pending or SuggestionState.Confirmed
            && suggestion.AmountMinor == alert.AmountMinor
            && string.Equals(suggestion.CurrencyCode, alert.CurrencyCode, StringComparison.Ordinal)
            && SameMovement(suggestion.Direction, alert.Direction)
            && (suggestion.Evidence & alert.Channel.AsEvidence()) == AlertEvidence.None
            && (suggestion.CreatedAt - receivedAt).Duration() <= Window;
    }

    /// <summary>The suggestion this alert belongs to, nearest in time, or null.</summary>
    /// <param name="recent">Recent suggestions, pending or confirmed.</param>
    /// <param name="alert">The new alert.</param>
    /// <param name="receivedAt">When the new alert arrived.</param>
    public static CaptureSuggestion? FindPartner(
        IEnumerable<CaptureSuggestion> recent,
        ParsedAlert alert,
        DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(recent);

        return recent
            .Where(s => Matches(s, alert, receivedAt))
            .OrderBy(s => (s.CreatedAt - receivedAt).Duration())
            .FirstOrDefault();
    }

    // A card bill paid through an app is a Self Transfer to the app, and a plain Debit to
    // the bank, whose SMS cannot tell a card from a shop. Those two describe one movement.
    private static bool SameMovement(Transactions.TransactionKind held, Transactions.TransactionKind incoming) =>
        held == incoming
        || (held, incoming) is (Transactions.TransactionKind.Debit, Transactions.TransactionKind.SelfTransfer)
            or (Transactions.TransactionKind.SelfTransfer, Transactions.TransactionKind.Debit);
}
