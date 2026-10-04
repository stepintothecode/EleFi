using EleFi.Domain.Containers;

namespace EleFi.Domain.Tests.Containers;

/// <summary>
/// The order containers appear in every picker: cards, then banks, then everything else.
/// </summary>
public class ContainerGroupingTests
{
    [Fact]
    public void Groups_come_out_cards_first_then_banks_then_the_rest()
    {
        var groups = ContainerGrouping.ForPicker(
        [
            Make("Wallet", ContainerKind.Wallet),
            Make("SBI", ContainerKind.BankAccount),
            Make("FD", ContainerKind.FixedDeposit),
            Make("Amex", ContainerKind.CreditCard),
            Make("Cash", ContainerKind.Cash),
        ]);

        Assert.Equal(
            [ContainerPickerGroup.CreditCards, ContainerPickerGroup.BankAccounts,
             ContainerPickerGroup.CashAndWallets, ContainerPickerGroup.SavingsAndInvestments],
            groups.Select(g => g.Group));
    }

    [Fact]
    public void Within_a_group_the_order_it_was_handed_is_kept()
    {
        // The repository already sorts by the user's own order and then by name. Grouping
        // must not quietly re-sort on top of that.
        var groups = ContainerGrouping.ForPicker(
        [
            Make("Zeta card", ContainerKind.CreditCard),
            Make("Alpha card", ContainerKind.CreditCard),
        ]);

        Assert.Equal(["Zeta card", "Alpha card"], groups[0].Containers.Select(c => c.Name));
    }

    [Fact]
    public void An_empty_group_is_left_out_rather_than_shown_as_a_bare_heading()
    {
        var groups = ContainerGrouping.ForPicker([Make("Cash", ContainerKind.Cash)]);

        Assert.Single(groups);
        Assert.Equal(ContainerPickerGroup.CashAndWallets, groups[0].Group);
    }

    [Theory]
    [InlineData(ContainerKind.CreditCard, ContainerPickerGroup.CreditCards)]
    [InlineData(ContainerKind.BankAccount, ContainerPickerGroup.BankAccounts)]
    [InlineData(ContainerKind.Cash, ContainerPickerGroup.CashAndWallets)]
    [InlineData(ContainerKind.Wallet, ContainerPickerGroup.CashAndWallets)]
    [InlineData(ContainerKind.FixedDeposit, ContainerPickerGroup.SavingsAndInvestments)]
    [InlineData(ContainerKind.RecurringDeposit, ContainerPickerGroup.SavingsAndInvestments)]
    [InlineData(ContainerKind.Ppf, ContainerPickerGroup.SavingsAndInvestments)]
    [InlineData(ContainerKind.MutualFund, ContainerPickerGroup.SavingsAndInvestments)]
    [InlineData(ContainerKind.Stocks, ContainerPickerGroup.SavingsAndInvestments)]
    [InlineData(ContainerKind.Nps, ContainerPickerGroup.SavingsAndInvestments)]
    public void Every_kind_lands_in_exactly_one_group(ContainerKind kind, ContainerPickerGroup expected)
    {
        Assert.Equal(expected, kind.PickerGroup());
    }

    [Fact]
    public void Every_group_has_a_heading()
    {
        foreach (var group in Enum.GetValues<ContainerPickerGroup>())
        {
            Assert.False(string.IsNullOrWhiteSpace(group.Heading()));
        }
    }

    [Fact]
    public void The_flat_picker_order_starts_with_the_first_card()
    {
        var ordered = ContainerGrouping.InPickerOrder(
        [
            Make("SBI", ContainerKind.BankAccount),
            Make("Amex", ContainerKind.CreditCard),
        ]);

        // This is what the form pre-selects, so it has to agree with what is drawn first.
        Assert.Equal(["Amex", "SBI"], ordered.Select(c => c.Name));
    }

    private static Container Make(string name, ContainerKind kind) => new() { Name = name, Kind = kind };
}
