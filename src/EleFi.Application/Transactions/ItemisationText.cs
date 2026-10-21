using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Transactions;

/// <summary>What Ele says when items do not fit their transaction.</summary>
/// <remarks>
/// One wording, used by the editor as an item is typed and by the service on save, so the user
/// never sees the same problem described two ways.
/// </remarks>
public static class ItemisationText
{
    /// <summary>The problem with an outcome, or null when it succeeded.</summary>
    /// <param name="outcome">What balancing came to.</param>
    /// <param name="totalMinor">The transaction's amount.</param>
    /// <param name="currency">Its currency.</param>
    public static string? Problem(ItemisationOutcome outcome, long totalMinor, Currency currency)
    {
        if (outcome.HasEmptyAmount)
        {
            return "Each item needs an amount above zero.";
        }

        return outcome.OverByMinor > 0
            ? $"The items come to {Show(outcome.OverByMinor, currency)} more than the {Show(totalMinor, currency)} paid."
            : null;
    }

    private static string Show(long minor, Currency currency) => MoneyText.ToDisplayString(Money.FromMinor(minor, currency));
}
