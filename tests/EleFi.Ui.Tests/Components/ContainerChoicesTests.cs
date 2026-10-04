using Bunit;
using EleFi.Domain.Containers;
using EleFi.Ui.Components;

namespace EleFi.Ui.Tests.Components;

/// <summary>Quick capture's From step: the same sections as the full form, as chips.</summary>
public class ContainerChoicesTests : Bunit.TestContext
{
    private static readonly Container Sbi = new() { Name = "SBI", Kind = ContainerKind.BankAccount };
    private static readonly Container Amex = new() { Name = "Amex", Kind = ContainerKind.CreditCard };

    [Fact]
    public void Containers_are_sectioned_cards_first()
    {
        var component = RenderComponent<ContainerChoices>(p => p.Add(x => x.Containers, [Sbi, Amex]));

        Assert.Equal(
            ["Credit cards", "Bank accounts"],
            component.FindAll(".choice-heading").Select(h => h.TextContent));
    }

    [Fact]
    public void Tapping_a_chip_chooses_it_and_the_chosen_one_is_marked()
    {
        var chosen = Guid.Empty;
        var component = RenderComponent<ContainerChoices>(p => p
            .Add(x => x.Containers, [Sbi, Amex])
            .Add(x => x.Selected, Amex.Id)
            .Add(x => x.OnChosen, id => chosen = id));

        Assert.Equal("true", component.Find("button[aria-pressed=true]").GetAttribute("aria-pressed"));
        Assert.Equal("Amex", component.Find("button[aria-pressed=true]").TextContent);

        component.FindAll("button")[1].Click();

        Assert.Equal(Sbi.Id, chosen);
    }
}
