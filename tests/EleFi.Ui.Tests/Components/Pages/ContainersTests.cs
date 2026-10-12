using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Containers;
using EleFi.Domain.Balances;
using EleFi.Domain.Containers;
using EleFi.Domain.Money;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>The containers screen: the list Home used to show, each row opening its sheet.</summary>
public class ContainersTests : Bunit.TestContext
{
    private readonly Container _hdfc = new()
    {
        Name = "HDFC",
        Kind = ContainerKind.BankAccount,
        InstitutionName = "HDFC Bank",
        AccountNumberLast4 = "4417",
    };

    [Fact]
    public void Add_opens_a_sheet_with_a_close_cross()
    {
        Register();
        var page = RenderComponent<Containers>();

        Assert.Empty(page.FindAll("[role=dialog]"));

        page.Find(".section-head button.add-container").Click();

        var dialog = page.Find("[role=dialog]");
        Assert.Equal("Add a container", dialog.GetAttribute("aria-label"));

        page.Find("[aria-label=Close]").Click();
        Assert.Empty(page.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Rows_carry_no_edit_or_delete_buttons_and_show_bank_and_last_four()
    {
        Register(detail: "HDFC Bank ••4417");
        var page = RenderComponent<Containers>();

        Assert.Empty(page.FindAll("button[aria-label='Edit HDFC']"));
        Assert.Empty(page.FindAll("button[aria-label='Delete HDFC']"));
        Assert.Equal("HDFC Bank ••4417", page.Find(".txn .detail").TextContent);
    }

    [Fact]
    public void Delete_and_archive_live_inside_the_container_sheet()
    {
        Register();
        var page = RenderComponent<Containers>();

        page.Find("button[aria-label='Open HDFC']").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[role=dialog] button.archive")));

        page.Find("[role=dialog] button.delete").Click();

        page.WaitForAssertion(() => Assert.Equal("Delete this container?", page.Find("[role=dialog]").GetAttribute("aria-label")));
    }

    [Fact]
    public void Tapping_a_row_opens_a_sheet_with_what_is_stored_already_filled_in()
    {
        Register();
        var page = RenderComponent<Containers>();

        page.Find("button[aria-label='Open HDFC']").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Equal("Edit container", page.Find("[role=dialog]").GetAttribute("aria-label"));

            // These used to open blank, and saving wrote the blanks over the stored values.
            Assert.Equal("HDFC Bank", page.Find("#institution").GetAttribute("value"));
            Assert.Equal("4417", page.Find("#last4").GetAttribute("value"));
        });
    }

    private void Register(string? detail = null)
    {
        var clock = new Fakes.StoppedClock();
        var containers = Substitute.For<IContainerRepository>();
        containers.ListAsync(Arg.Any<CancellationToken>()).Returns([_hdfc]);

        var transactions = Substitute.For<ITransactionRepository>();
        transactions.BalancesAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new ContainerBalance(_hdfc.Id, _hdfc.Name, _hdfc.Kind, Money.FromMinor(10_000_00, Currency.Inr), detail),
        ]);

        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton(new ContainerService(containers, transactions, clock));
        Services.AddEleAndUndo();
    }
}
