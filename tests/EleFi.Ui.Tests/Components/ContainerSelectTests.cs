using Bunit;
using EleFi.Domain.Containers;
using EleFi.Ui.Components;

namespace EleFi.Ui.Tests.Components;

/// <summary>The "Paid from" dropdown, sectioned rather than alphabetical.</summary>
public class ContainerSelectTests : Bunit.TestContext
{
    private static readonly Container Sbi = new() { Name = "SBI", Kind = ContainerKind.BankAccount };
    private static readonly Container Amex = new() { Name = "Amex", Kind = ContainerKind.CreditCard };
    private static readonly Container Cash = new() { Name = "Cash", Kind = ContainerKind.Cash };

    [Fact]
    public void Cards_come_first_then_banks_then_the_rest_each_under_a_heading()
    {
        var component = RenderComponent<ContainerSelect>(p => p.Add(x => x.Containers, [Sbi, Cash, Amex]));

        var groups = component.FindAll("optgroup");

        Assert.Equal(["Credit cards", "Bank accounts", "Cash and wallets"], groups.Select(g => g.GetAttribute("label")));
        Assert.Equal(["Amex", "SBI", "Cash"], component.FindAll("option").Select(o => o.TextContent));
    }

    [Fact]
    public void With_nothing_chosen_the_placeholder_asks_rather_than_defaulting()
    {
        var component = RenderComponent<ContainerSelect>(p => p
            .Add(x => x.Containers, [Sbi, Amex])
            .Add(x => x.Placeholder, "Choose one"));

        var first = component.Find("option");
        Assert.Equal("Choose one", first.TextContent);
        Assert.Equal(string.Empty, first.GetAttribute("value"));
    }

    [Fact]
    public void Choosing_an_option_reports_the_container()
    {
        var chosen = Guid.Empty;
        var component = RenderComponent<ContainerSelect>(p => p
            .Add(x => x.Containers, [Sbi, Amex])
            .Add(x => x.ValueChanged, id => chosen = id));

        component.Find("select").Change(Sbi.Id.ToString());

        Assert.Equal(Sbi.Id, chosen);
    }
}
