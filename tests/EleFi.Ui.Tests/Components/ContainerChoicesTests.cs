using Bunit;
using EleFi.Domain.Containers;
using EleFi.Ui.Components;

namespace EleFi.Ui.Tests.Components;

/// <summary>Quick capture's From step: the same order as the full form, as chips.</summary>
public class ContainerChoicesTests : Bunit.TestContext
{
    private static readonly Container Sbi = new() { Name = "SBI", Kind = ContainerKind.BankAccount, InstitutionName = "SBI", AccountNumberLast4 = "9001" };
    private static readonly Container Amex = new() { Name = "Amex", Kind = ContainerKind.CreditCard };

    [Fact]
    public void Containers_come_cards_first_with_no_headings()
    {
        var component = RenderComponent<ContainerChoices>(p => p.Add(x => x.Containers, [Sbi, Amex]));

        Assert.Equal(["Amex", "SBI"], component.FindAll("button > span").Select(s => s.TextContent));
        Assert.DoesNotContain("Credit cards", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_chip_carries_its_bank_and_last_four_under_the_name()
    {
        var component = RenderComponent<ContainerChoices>(p => p.Add(x => x.Containers, [Sbi]));

        Assert.Equal("SBI ••9001", component.Find("button small.detail").TextContent);
    }

    [Fact]
    public void Tapping_a_chip_chooses_it_and_the_chosen_one_is_marked()
    {
        var chosen = Guid.Empty;
        var component = RenderComponent<ContainerChoices>(p => p
            .Add(x => x.Containers, [Sbi, Amex])
            .Add(x => x.Selected, Amex.Id)
            .Add(x => x.OnChosen, id => chosen = id));

        Assert.Equal("Amex", component.Find("button[aria-pressed=true] > span").TextContent);

        component.FindAll("button")[1].Click();

        Assert.Equal(Sbi.Id, chosen);
    }
}
