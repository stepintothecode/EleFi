using EleFi.Domain.Balances;
using EleFi.Domain.Containers;
using EleFi.Domain.Money;
using Cash = EleFi.Domain.Money.Money;

namespace EleFi.Domain.Tests.Balances;

/// <summary>
/// Which part of net worth each container counts toward.
/// </summary>
public class BalanceMathTests
{
    [Theory]
    [InlineData(ContainerKind.BankAccount, NetWorthPart.Liquid)]
    [InlineData(ContainerKind.Cash, NetWorthPart.Liquid)]
    [InlineData(ContainerKind.FixedDeposit, NetWorthPart.Locked)]
    [InlineData(ContainerKind.MutualFund, NetWorthPart.Locked)]
    [InlineData(ContainerKind.CreditCard, NetWorthPart.Owed)]
    public void Each_kind_counts_toward_one_part(ContainerKind kind, NetWorthPart expected)
    {
        var balance = new ContainerBalance(Guid.NewGuid(), "Any", kind, Cash.FromMinor(100, Currency.Inr));

        Assert.Equal(expected, BalanceMath.PartOf(balance));
    }

    [Fact]
    public void The_containers_listed_under_a_part_add_up_to_that_part()
    {
        // The dashboard lists the containers behind each figure. If the list and the figure
        // used different rules, tapping Liquid could show containers that do not add up to it.
        ContainerBalance[] balances =
        [
            new(Guid.NewGuid(), "HDFC", ContainerKind.BankAccount, Cash.FromMinor(50_000_00, Currency.Inr)),
            new(Guid.NewGuid(), "Wallet", ContainerKind.Cash, Cash.FromMinor(2_000_00, Currency.Inr)),
            new(Guid.NewGuid(), "FD", ContainerKind.FixedDeposit, Cash.FromMinor(1_00_000_00, Currency.Inr)),
            new(Guid.NewGuid(), "Card", ContainerKind.CreditCard, Cash.SignedMinor(-12_000_00, Currency.Inr)),
        ];

        var total = BalanceMath.Summarise(balances, Currency.Inr);

        Assert.Equal(total.Liquid.Minor, balances.Where(b => BalanceMath.PartOf(b) == NetWorthPart.Liquid).Sum(b => b.Balance.Minor));
        Assert.Equal(total.Locked.Minor, balances.Where(b => BalanceMath.PartOf(b) == NetWorthPart.Locked).Sum(b => b.Balance.Minor));
        Assert.Equal(total.Owed.Minor, balances.Where(b => BalanceMath.PartOf(b) == NetWorthPart.Owed).Sum(b => b.AmountOwed.Minor));
    }
}
