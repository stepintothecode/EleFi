using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Contacts;
using EleFi.Domain.Filters;
using EleFi.Domain.Parties;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>One contact: details, notes, other names, links, money, and the way to its transactions.</summary>
public class ContactPageTests : Bunit.TestContext
{
    private readonly IContactRepository _contacts = Substitute.For<IContactRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly Party _rahul = new() { Kind = PartyKind.External, Name = "Rahul", Notes = "Lent him the drill" };
    private readonly Party _rahulSbi = new() { Kind = PartyKind.External, Name = "Rahul SBI" };
    private readonly List<ContactDetail> _details = [];
    private readonly List<ContactLink> _links = [];
    private readonly Fakes.FakeContactPicker _picker = new();
    private readonly TransactionListState _list = new();

    public ContactPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        foreach (var party in new[] { _rahul, _rahulSbi })
        {
            _contacts.FindAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);
        }

        _contacts.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new ContactSummary(_rahul.Id, "Rahul", 3, null, 0, false),
            new ContactSummary(_rahulSbi.Id, "Rahul SBI", 1, null, 0, false),
        ]);
        _contacts.DetailsAsync(_rahul.Id, Arg.Any<CancellationToken>()).Returns(_ => _details.ToList());
        _contacts.When(c => c.AddDetailAsync(Arg.Any<ContactDetail>(), Arg.Any<CancellationToken>()))
            .Do(call => _details.Add(call.Arg<ContactDetail>()));
        _contacts.LinksAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(_ => _links.ToList());
        _contacts.When(c => c.AddLinkAsync(Arg.Any<ContactLink>(), Arg.Any<CancellationToken>()))
            .Do(call => _links.Add(call.Arg<ContactLink>()));
        _contacts.AliasesAsync(_rahul.Id, Arg.Any<CancellationToken>()).Returns(["RAHUL K"]);

        _transactions.CountAsync(Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>()).Returns(3);
        _transactions.TotalsAsync(Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>()).Returns(new FlowTotals(500_00, 1_200_00));

        Services.AddSingleton(new ContactService(_contacts, _transactions, new Fakes.StoppedClock()));
        Services.AddSingleton<IContactPicker>(_picker);
        Services.AddEleAndUndo();
        Services.AddSingleton(_list);
    }

    [Fact]
    public void The_page_shows_the_money_both_ways_the_notes_and_the_other_names()
    {
        var page = Render();

        Assert.Contains("₹1,200.00", page.Find(".contact-money").TextContent, StringComparison.Ordinal);
        Assert.Contains("₹500.00", page.Find(".contact-money").TextContent, StringComparison.Ordinal);
        Assert.Equal("Lent him the drill", page.Find("#contact-notes").GetAttribute("value") ?? page.Find("#contact-notes").TextContent);
        Assert.Contains("RAHUL K", page.Find(".contact-aliases").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_phone_number_is_added_and_shown()
    {
        var page = Render();

        page.Find("button.add-detail").Click();
        page.Find("#detail-value").Input("+91 98765 43210");
        page.Find("[role=dialog] button.primary").Click();

        page.WaitForAssertion(() =>
            Assert.Contains("+91 98765 43210", page.Find(".contact-details").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void A_detail_that_is_not_what_it_says_is_explained_and_not_kept()
    {
        var page = Render();

        page.Find("button.add-detail").Click();
        page.FindAll("[role=dialog] .chip").First(c => c.TextContent.Trim() == "Email").Click();
        page.Find("#detail-value").Input("rahul at gmail");
        page.Find("[role=dialog] button.primary").Click();

        Assert.Empty(_details);
        var ele = Services.GetRequiredService<EleReactions>().Current!;
        Assert.Equal(EleTone.Cross, ele.Tone);
        Assert.Contains("email", ele.Line, StringComparison.OrdinalIgnoreCase);

        // The sheet stays open so the value can be fixed.
        Assert.NotNull(page.Find("#detail-value"));
    }

    [Fact]
    public void Show_transactions_opens_the_list_filtered_to_this_contact_and_those_linked_to_it()
    {
        _links.Add(ContactLink.Between(_rahul.Id, _rahulSbi.Id, ContactRelation.AnotherAccountOf, DateTimeOffset.UnixEpoch));
        var page = Render();

        page.Find("button.show-transactions").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Equal(new[] { _rahul.Id, _rahulSbi.Id }.Order(), _list.Filter.PartyIds.Order());
            Assert.EndsWith("/transactions", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
            Assert.Equal("Rahul and 1 linked", _list.PartyLabel);
        });
    }

    [Fact]
    public void Another_contact_can_be_linked_as_another_account()
    {
        var page = Render();

        page.Find("button.add-link").Click();
        page.Find("[role=dialog] select").Change(_rahulSbi.Id.ToString());
        page.FindAll("[role=dialog] .chip").First(c => c.TextContent.Trim() == "Another account of").Click();
        page.Find("[role=dialog] button.primary").Click();

        page.WaitForAssertion(() =>
        {
            var link = Assert.Single(_links);
            Assert.Equal(ContactRelation.AnotherAccountOf, link.Relation);
            Assert.Contains("Rahul SBI", page.Find(".contact-links").TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void A_linked_phone_contact_opens_in_the_phones_contacts()
    {
        _rahul.PhoneContactKey = "lookup-7";
        var page = Render();

        page.Find("button.open-phone-contact").Click();

        Assert.Equal(["lookup-7"], _picker.Opened);
    }

    [Fact]
    public void Without_a_contacts_picker_on_this_device_there_is_no_link_to_one()
    {
        Services.AddSingleton<IContactPicker>(new Fakes.FakeContactPicker { IsSupported = false });
        var page = Render();

        Assert.Empty(page.FindAll("button.link-phone-contact"));
    }

    [Fact]
    public void Renaming_keeps_the_old_name_by_default()
    {
        var page = Render();

        page.Find("button.rename").Click();
        page.Find("#contact-name").Input("Rahul Sharma");
        page.Find("[role=dialog] button.primary").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Equal("Rahul Sharma", _rahul.Name);
            _contacts.Received(1).AddAliasAsync(_rahul.Id, "Rahul", Arg.Any<CancellationToken>());
        });
    }

    private IRenderedComponent<ContactPage> Render()
    {
        var page = RenderComponent<ContactPage>(p => p.Add(c => c.Id, _rahul.Id));
        page.WaitForAssertion(() => Assert.NotNull(page.Find(".contact-money")));
        return page;
    }
}
