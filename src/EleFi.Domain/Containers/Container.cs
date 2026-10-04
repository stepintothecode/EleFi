using EleFi.Domain.Money;
using EleFi.Domain.Primitives;

namespace EleFi.Domain.Containers;

/// <summary>
/// A Money Container: anything that holds the user's money.
/// </summary>
/// <remarks>
/// Never called an account. "Account" means a login identity, or the specific
/// <see cref="ContainerKind.BankAccount"/> kind, and using it for the general concept is
/// the most common way this model gets muddled.
/// <para>
/// There is no balance field here, deliberately. A balance is always derived from the
/// opening balance plus every transaction that touched the container (D2). A stored
/// balance would need rewinding and replaying on every back-dated entry, and any bug in
/// that leaves a silently wrong number.
/// </para>
/// </remarks>
public class Container : Entity
{
    /// <summary>What the user calls it: "HDFC Savings".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Which of the seven kinds this is. Drives liability and liquidity.</summary>
    public ContainerKind Kind { get; set; }

    /// <summary>The ISO-4217 code this container is denominated in.</summary>
    /// <remarks>Immutable once transactions exist (C5): changing it would reinterpret every historical amount.</remarks>
    public string CurrencyCode { get; set; } = Currency.Inr.Code;

    /// <summary>What it held on <see cref="OpeningBalanceAsOf"/>, in minor units. May be negative for a card.</summary>
    public long OpeningBalanceMinor { get; set; }

    /// <summary>The date the user started tracking this container.</summary>
    public DateOnly OpeningBalanceAsOf { get; set; }

    /// <summary>Hidden from pickers, still counted in history and reports (C4).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Manual ordering in the container list.</summary>
    public int SortOrder { get; set; }

    /// <summary>A hex colour for the UI, or null for the default.</summary>
    public string? Colour { get; set; }

    /// <summary>An icon name for the UI, or null for the kind's default.</summary>
    public string? Icon { get; set; }

    /// <summary>The bank or issuer, for bank-like kinds.</summary>
    public string? InstitutionName { get; set; }

    /// <summary>
    /// The last four digits of the account or card number. Never more (C1).
    /// </summary>
    /// <remarks>
    /// A full number cannot help the user and is the single most damaging field to leak.
    /// The setter truncates rather than trusting callers, because a validation that lives
    /// in one place is the only kind that holds.
    /// </remarks>
    public string? AccountNumberLast4
    {
        get;
        set => field = Truncate(value);
    }

    /// <summary>The IFSC code, for Indian bank accounts.</summary>
    public string? Ifsc { get; set; }

    /// <summary>The credit limit in minor units. Credit cards only (C2).</summary>
    public long? CreditLimitMinor { get; set; }

    /// <summary>Day of month the statement is generated. Credit cards only.</summary>
    public int? StatementDay { get; set; }

    /// <summary>Day of month the payment is due. Credit cards only.</summary>
    public int? PaymentDueDay { get; set; }

    /// <summary>Interest rate in basis points: 750 is 7.50%. Deposits only (C3).</summary>
    public int? InterestRateBps { get; set; }

    /// <summary>When the deposit matures. Informational only; interest is never synthesised.</summary>
    public DateOnly? MaturityDate { get; set; }

    /// <summary>Tenure in months, for deposits.</summary>
    public int? TenureMonths { get; set; }

    /// <summary>The monthly installment for a recurring deposit, in minor units.</summary>
    public long? InstallmentMinor { get; set; }

    /// <summary>The currency, as a value type.</summary>
    public Currency Currency => Currency.Of(CurrencyCode);

    /// <summary>True when the balance means money owed.</summary>
    public bool IsLiability => Kind.IsLiability();

    /// <summary>True when the money is spendable this week.</summary>
    public bool IsLiquid => Kind.IsLiquid();

    /// <summary>The opening balance as a signed amount.</summary>
    public Money.Money OpeningBalance => Money.Money.SignedMinor(OpeningBalanceMinor, Currency);

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        return digits.Length <= 4 ? digits : digits[^4..];
    }
}
