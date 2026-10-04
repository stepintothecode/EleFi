namespace EleFi.Domain.Containers;

/// <summary>The sections a container picker is divided into, in the order they are drawn.</summary>
public enum ContainerPickerGroup
{
    /// <summary>Credit cards. First, because most day-to-day spending goes on one.</summary>
    CreditCards = 0,

    /// <summary>Savings and current accounts.</summary>
    BankAccounts = 1,

    /// <summary>Notes and coins, and stored balances such as Paytm or a gift card.</summary>
    CashAndWallets = 2,

    /// <summary>Deposits and investments. Rarely a Source, so they sit last.</summary>
    SavingsAndInvestments = 3,
}

/// <summary>One section of a container picker.</summary>
/// <param name="Group">Which section this is.</param>
/// <param name="Heading">What the section is called on screen.</param>
/// <param name="Containers">The containers in it, in the order they were handed in.</param>
public sealed record ContainerGroup(
    ContainerPickerGroup Group,
    string Heading,
    IReadOnlyList<Container> Containers);

/// <summary>
/// Orders containers for a picker: cards, then banks, then cash and wallets, then the rest.
/// </summary>
/// <remarks>
/// A flat alphabetical list put "Amex" next to "Axis FD" and made the user read every row to
/// find the card they had just tapped. Grouping by what the container is, in the order it is
/// most often the Source, means the likely answer is near the top. Within a group the
/// incoming order is kept, so the user's own sort order still wins.
/// </remarks>
public static class ContainerGrouping
{
    /// <summary>Which picker section a kind belongs to.</summary>
    /// <param name="kind">The container kind.</param>
    public static ContainerPickerGroup PickerGroup(this ContainerKind kind) => kind switch
    {
        ContainerKind.CreditCard => ContainerPickerGroup.CreditCards,
        ContainerKind.BankAccount => ContainerPickerGroup.BankAccounts,
        ContainerKind.Cash or ContainerKind.Wallet => ContainerPickerGroup.CashAndWallets,
        _ => ContainerPickerGroup.SavingsAndInvestments,
    };

    /// <summary>The heading drawn above a picker section.</summary>
    /// <param name="group">The section.</param>
    public static string Heading(this ContainerPickerGroup group) => group switch
    {
        ContainerPickerGroup.CreditCards => "Credit cards",
        ContainerPickerGroup.BankAccounts => "Bank accounts",
        ContainerPickerGroup.CashAndWallets => "Cash and wallets",
        ContainerPickerGroup.SavingsAndInvestments => "Savings and investments",
        _ => group.ToString(),
    };

    /// <summary>Splits containers into picker sections, leaving out empty ones.</summary>
    /// <param name="containers">The containers, already in the user's preferred order.</param>
    public static IReadOnlyList<ContainerGroup> ForPicker(IEnumerable<Container> containers)
    {
        ArgumentNullException.ThrowIfNull(containers);

        // GroupBy keeps first-seen order within each group, which is what preserves the
        // user's sort order inside a section.
        return containers
            .GroupBy(c => c.Kind.PickerGroup())
            .OrderBy(g => g.Key)
            .Select(g => new ContainerGroup(g.Key, g.Key.Heading(), g.ToList()))
            .ToList();
    }

    /// <summary>The same containers flattened in picker order, for choosing a default.</summary>
    /// <param name="containers">The containers, already in the user's preferred order.</param>
    public static IReadOnlyList<Container> InPickerOrder(IEnumerable<Container> containers) =>
        ForPicker(containers).SelectMany(g => g.Containers).ToList();
}
