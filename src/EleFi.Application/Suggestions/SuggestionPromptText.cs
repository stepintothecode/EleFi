using EleFi.Application.Abstractions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Suggestions;

/// <summary>Words for a Suggestion Prompt.</summary>
/// <remarks>
/// A question, never a statement. "₹450 to Zomato?" says the app thinks this happened and
/// is asking; "Paid ₹450 to Zomato" would say it has been recorded, which it has not (SM2).
/// </remarks>
public static class SuggestionPromptText
{
    /// <summary>The prompt for a suggestion.</summary>
    /// <param name="suggestion">The suggestion.</param>
    /// <param name="containerName">The matched container's name, if one was matched.</param>
    public static SuggestionPrompt For(CaptureSuggestion suggestion, string? containerName)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        var amount = MoneyText.ToDisplayString(suggestion.Amount);
        var who = string.IsNullOrWhiteSpace(suggestion.CounterpartyText) ? null : suggestion.CounterpartyText.Trim();
        var title = (suggestion.Direction, who) switch
        {
            (TransactionKind.SelfTransfer, not null) => $"{amount} paid to your {who}?",
            (TransactionKind.SelfTransfer, null) => $"{amount} card bill paid?",
            (TransactionKind.Debit, not null) => $"{amount} to {who}?",
            (TransactionKind.Debit, null) => $"{amount} spent?",
            (_, not null) => $"{amount} from {who}?",
            _ => $"{amount} received?",
        };

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(containerName))
        {
            parts.Add($"{(suggestion.Direction == TransactionKind.Credit ? "Into" : "From")} {containerName}");
        }

        if (!string.IsNullOrWhiteSpace(suggestion.PaymentAppName))
        {
            parts.Add($"via {suggestion.PaymentAppName}");
        }

        if (!string.IsNullOrWhiteSpace(suggestion.Note))
        {
            parts.Add($"\"{suggestion.Note}\"");
        }

        var detail = parts.Count == 0 ? string.Empty : string.Join(" ", parts) + ". ";
        return new SuggestionPrompt(suggestion.Id, title, $"{detail}Not recorded yet. Tap to check and add it.");
    }
}
