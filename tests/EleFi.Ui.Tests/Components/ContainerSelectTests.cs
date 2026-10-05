using Bunit;
using EleFi.Domain.Containers;
using EleFi.Ui.Components;

namespace EleFi.Ui.Tests.Components;

/// <summary>The "Paid from" picker: ordered by kind, no headings, bank and last four under each name.</summary>
public class ContainerSelectTests : Bunit.TestContext
{
    private static readonly Container Sbi = new() { Name = "SBI", Kind = ContainerKind.BankAccount, InstitutionName = "SBI", AccountNumberLast4 = "9001" };
    private static readonly Container Amex = new() { Name = "Amex", Kind = ContainerKind.CreditCard };
    private static readonly Container Cash = new() { Name = "Cash", Kind = ContainerKind.Cash };

    [Fact]
    public void The_field_shows_the_choice_with_its_bank_and_last_four_underneath()
    {
        var component = RenderComponent<ContainerSelect>(p => p
            .Add(x => x.Containers, [Sbi, Amex])
            .Add(x => x.Value, Sbi.Id));

        Assert.Equal("SBI", component.Find(".picker .picker-name").TextContent);
        Assert.Equal("SBI ••9001", component.Find(".picker .detail").TextContent);
    }

    [Fact]
    public void Options_come_cards_first_then_banks_then_the_rest_with_no_headings()
    {
        var component = RenderComponent<ContainerSelect>(p => p
            .Add(x => x.Containers, [Sbi, Cash, Amex])
            .Add(x => x.Title, "Paid from"));

        component.Find(".picker").Click();

        Assert.Equal("Paid from", component.Find("[role=dialog]").GetAttribute("aria-label"));
        Assert.Equal(["Amex", "SBI", "Cash"], component.FindAll("[role=option] .picker-name").Select(o => o.TextContent));

        // The order says it; the names of the kinds would only repeat it.
        Assert.DoesNotContain("Credit cards", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Bank accounts", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Choosing_an_option_reports_it_and_closes_the_sheet()
    {
        var chosen = Guid.Empty;
        var component = RenderComponent<ContainerSelect>(p => p
            .Add(x => x.Containers, [Sbi, Amex])
            .Add(x => x.Value, Amex.Id)
            .Add(x => x.ValueChanged, id => chosen = id));

        component.Find(".picker").Click();
        component.FindAll("[role=option]")[1].Click();

        Assert.Equal(Sbi.Id, chosen);
        Assert.Empty(component.FindAll("[role=dialog]"));
    }

    [Fact]
    public void With_nothing_chosen_the_placeholder_asks_rather_than_defaulting()
    {
        var component = RenderComponent<ContainerSelect>(p => p
            .Add(x => x.Containers, [Sbi, Amex])
            .Add(x => x.Placeholder, "Choose one"));

        Assert.Equal("Choose one", component.Find(".picker .picker-text").TextContent.Trim());
    }
}
