namespace EleFi.Domain.Containers;

/// <summary>
/// What sort of Money Container this is. Seven kinds, fixed.
/// </summary>
public enum ContainerKind
{
    /// <summary>A savings or current account at a bank.</summary>
    BankAccount = 0,

    /// <summary>A credit card. The only liability kind: its balance is what is owed.</summary>
    CreditCard = 1,

    /// <summary>Physical cash on hand.</summary>
    Cash = 2,

    /// <summary>Any other holder of money: a payment wallet, a piggy bank.</summary>
    Wallet = 3,

    /// <summary>A fixed deposit. Real wealth, not spendable this week.</summary>
    FixedDeposit = 4,

    /// <summary>A recurring deposit.</summary>
    RecurringDeposit = 5,

    /// <summary>A Public Provident Fund account.</summary>
    Ppf = 6,

    /// <summary>
    /// A mutual fund holding.
    /// </summary>
    /// <remarks>
    /// The balance is what you have <b>put in</b>, not what it is worth today. EleFi derives
    /// every balance from transactions (D2) and has no price feed, so an investment
    /// container tracks cost basis. Market value would need daily NAV data, a network call,
    /// and a whole valuation model.
    /// </remarks>
    MutualFund = 7,

    /// <summary>Shares held in a demat account. Cost basis, like <see cref="MutualFund"/>.</summary>
    Stocks = 8,

    /// <summary>A National Pension System account.</summary>
    Nps = 9,
}

/// <summary>
/// The traits derived from a container's kind.
/// </summary>
/// <remarks>
/// These are computed, never stored. Storing them would allow a credit card marked as an
/// asset to exist, and there is no reason to permit that state.
/// </remarks>
public static class ContainerKindTraits
{
    /// <summary>
    /// True when the balance is money owed rather than money held. Only a credit card.
    /// </summary>
    /// <param name="kind">The container kind.</param>
    public static bool IsLiability(this ContainerKind kind) => kind == ContainerKind.CreditCard;

    /// <summary>
    /// True when the money is spendable this week.
    /// </summary>
    /// <remarks>
    /// Deposits and PPF are real wealth but cannot be spent on demand, which is why the
    /// dashboard separates Liquid from Locked instead of showing one total that hides it.
    /// </remarks>
    /// <param name="kind">The container kind.</param>
    public static bool IsLiquid(this ContainerKind kind) => kind switch
    {
        ContainerKind.FixedDeposit or ContainerKind.RecurringDeposit or ContainerKind.Ppf
            or ContainerKind.MutualFund or ContainerKind.Stocks or ContainerKind.Nps => false,
        _ => true,
    };

    /// <summary>
    /// True when the balance is what was put in rather than what it is worth now.
    /// </summary>
    /// <remarks>
    /// Balances are derived from transactions and EleFi has no price feed, so an investment
    /// container holds cost basis. Saying so in the UI is the difference between a number
    /// the user can trust and one they quietly assume is their portfolio value.
    /// </remarks>
    public static bool TracksCostBasis(this ContainerKind kind) => kind switch
    {
        ContainerKind.MutualFund or ContainerKind.Stocks or ContainerKind.Nps => true,
        _ => false,
    };

    /// <summary>One line explaining the kind, shown beside it in the picker.</summary>
    /// <remarks>
    /// Cash and Wallet look interchangeable until you read these, which is why the picker
    /// shows them rather than hiding them behind a help screen.
    /// </remarks>
    public static string Description(this ContainerKind kind) => kind switch
    {
        ContainerKind.BankAccount => "A savings or current account",
        ContainerKind.CreditCard => "You owe the balance rather than hold it",
        ContainerKind.Cash => "Physical notes and coins you carry",
        ContainerKind.Wallet => "A stored balance you can only spend in one place: Paytm, PhonePe, a gift card, a piggy bank",
        ContainerKind.FixedDeposit => "Locked until maturity",
        ContainerKind.RecurringDeposit => "A fixed amount paid in every month",
        ContainerKind.Ppf => "Public Provident Fund. Locked for years",
        ContainerKind.MutualFund => "Tracks what you invested, not today's market value",
        ContainerKind.Stocks => "Tracks what you invested, not today's market value",
        ContainerKind.Nps => "National Pension System. Locked until retirement",
        _ => string.Empty,
    };

    /// <summary>True for bank-like kinds that carry an institution and an IFSC.</summary>
    /// <param name="kind">The container kind.</param>
    public static bool IsBankLike(this ContainerKind kind) => kind switch
    {
        ContainerKind.BankAccount or ContainerKind.CreditCard or ContainerKind.FixedDeposit
            or ContainerKind.RecurringDeposit or ContainerKind.Ppf => true,
        _ => false,
    };

    /// <summary>A short label for the UI.</summary>
    /// <param name="kind">The container kind.</param>
    public static string DisplayName(this ContainerKind kind) => kind switch
    {
        ContainerKind.BankAccount => "Bank account",
        ContainerKind.CreditCard => "Credit card",
        ContainerKind.Cash => "Cash",
        ContainerKind.Wallet => "Wallet",
        ContainerKind.FixedDeposit => "Fixed deposit",
        ContainerKind.RecurringDeposit => "Recurring deposit",
        ContainerKind.Ppf => "PPF",
        ContainerKind.MutualFund => "Mutual fund",
        ContainerKind.Stocks => "Stocks",
        ContainerKind.Nps => "NPS",
        _ => kind.ToString(),
    };
}
