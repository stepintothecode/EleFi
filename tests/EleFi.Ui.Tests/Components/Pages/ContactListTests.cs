using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Contacts;
using EleFi.Domain.Parties;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>Every contact so far, the way to the likely duplicates, and adding one.</summary>
public class ContactListTests : Bunit.TestContext
{
    private readonly IContactRepository _contacts = Substitute.For<IContactRepository>();
    private readonly List<ContactSummary> _rows = [];
    private readonly List<ContactLink> _links = [];
    private readonly Fakes.FakeContactPicker _picker = new();

    public ContactListTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _contacts.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => _rows.ToList());
        _contacts.LinksAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(_ => _links.ToList());
        _contacts.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call =>
            _rows.Find(r => r.Id == call.Arg<Guid>()) is { } row ? new Party { Id = row.Id, Kind = PartyKind.External, Name = row.Name } : null);
        _contacts.When(c => c.AddLinkAsync(Arg.Any<ContactLink>(), Arg.Any<CancellationToken>()))
            .Do(call => _links.Add(call.Arg<ContactLink>()));
        _contacts.AddAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new Party { Kind = PartyKind.External, Name = call.Arg<string>() });

        Services.AddSingleton(new ContactService(_contacts, Substitute.For<ITransactionRepository>(), new Fakes.StoppedClock()));
        Services.AddSingleton<IContactPicker>(_picker);
        Services.AddEleAndUndo();
    }

    [Fact]
    public void Every_contact_is_listed_with_how_often_it_has_been_used()
    {
        Add("Zomato", transactions: 12);
        Add("Rahul", transactions: 1);

        var page = RenderComponent<ContactList>();

        var rows = page.FindAll(".contact-row");
        Assert.Equal(["Rahul", "Zomato"], rows.Select(r => r.QuerySelector(".who")!.TextContent));
        Assert.Contains("12 transactions", rows[1].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Typing_a_name_narrows_the_list()
    {
        Add("Zomato");
        Add("Rahul");

        var page = RenderComponent<ContactList>();
        page.Find("#contact-search").Input("zom");

        Assert.Equal(["Zomato"], page.FindAll(".contact-row .who").Select(w => w.TextContent));
    }

    [Fact]
    public void Each_contact_opens_its_own_page()
    {
        var zomato = Add("Zomato");

        var page = RenderComponent<ContactList>();

        Assert.Equal($"contacts/{zomato.Id}", page.Find(".contact-row").GetAttribute("href"));
    }

    [Fact]
    public void Duplicates_have_their_own_page_behind_a_button_that_says_how_many_wait()
    {
        Add("Swiggy");
        Add("SWIGGY LTD");

        var page = RenderComponent<ContactList>();

        var button = page.Find(".section-head a.duplicates-link");
        Assert.Equal("settings/contacts/duplicates", button.GetAttribute("href"));
        Assert.Equal("1", button.QuerySelector(".bell-count")!.TextContent);
        Assert.Empty(page.FindAll(".duplicate"));
    }
    [Fact]
    public void A_new_contact_can_be_added_by_name()
    {
        var page = RenderComponent<ContactList>();

        page.Find(".section-head button.new-contact").Click();
        page.Find("#new-contact-name").Input("Priya");
        page.Find("[role=dialog] button.primary").Click();

        page.WaitForAssertion(() => _contacts.Received(1).AddAsync("Priya", Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void Picking_from_the_phone_adds_that_person_with_their_number()
    {
        _picker.Next = new PickedContact("Priya", "98765 43210", "lookup-1");
        var page = RenderComponent<ContactList>();

        page.Find(".section-head button.new-contact").Click();
        page.Find("[role=dialog] button.from-phone").Click();

        page.WaitForAssertion(() =>
        {
            _contacts.Received(1).AddAsync("Priya", Arg.Any<CancellationToken>());
            Assert.Contains("contacts/", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        });
    }

    private ContactSummary Add(string name, int transactions = 1)
    {
        var row = new ContactSummary(Guid.CreateVersion7(), name, transactions, null, 0, false);
        _rows.Add(row);
        _rows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return row;
    }
}
