using EleFi.Application.Abstractions;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Suggestions;

/// <summary>Words for an Alert Prompt.</summary>
/// <remarks>
/// A statement, because the payment has been recorded (ADR-0015), followed by the one thing
/// that matters next: it was read from a message, so it needs checking.
/// </remarks>
public static class AlertPromptText
{
    /// <summary>The prompt for a transaction an alert recorded.</summary>
    /// <param name="transaction">The transaction, with its parties loaded.</param>
    /// <param name="containerName">The container it was recorded against.</param>
    /// <param name="paymentAppName">The Payment App, when an app reported it.</param>
    public static AlertPrompt For(Transaction transaction, string? containerName, string? paymentAppName)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var amount = MoneyText.ToDisplayString(transaction.SourceAmount);
        var kind = transaction.Kind;

        var title = kind switch
        {
            TransactionKind.SelfTransfer => $"{amount} card bill paid",
            TransactionKind.Credit => $"{amount} from {transaction.SourceParty?.Name ?? "someone"}",
            _ => $"{amount} to {transaction.DestinationParty?.Name ?? "someone"}",
        };

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(containerName))
        {
            parts.Add($"{(kind == TransactionKind.Credit ? "Into" : "From")} {containerName}");
        }

        if (!string.IsNullOrWhiteSpace(paymentAppName))
        {
            parts.Add($"via {paymentAppName}");
        }

        if (!string.IsNullOrWhiteSpace(transaction.Description))
        {
            parts.Add($"\"{transaction.Description}\"");
        }

        var detail = parts.Count == 0 ? string.Empty : string.Join(" ", parts) + ". ";
        return new AlertPrompt(transaction.Id, title, $"{detail}Recorded, needs review. Tap to check it.");
    }
}
