using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Contacts;
using EleFi.Domain.Parties;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>Possible duplicates: Merge, Same, Different, and the way back from each.</summary>
public class PossibleDuplicatesTests : Bunit.TestContext
{
    private readonly IContactRepository _contacts = Substitute.For<IContactRepository>();
    private readonly List<ContactSummary> _rows = [];
    private readonly List<ContactLink> _links = [];

    public PossibleDuplicatesTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _contacts.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => _rows.ToList());
        _contacts.LinksAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(_ => _links.ToList());
        _contacts.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call =>
            _rows.Find(r => r.Id == call.Arg<Guid>()) is { } row ? new Party { Id = row.Id, Kind = PartyKind.External, Name = row.Name } : null);
        _contacts.When(c => c.AddLinkAsync(Arg.Any<ContactLink>(), Arg.Any<CancellationToken>()))
            .Do(call => _links.Add(call.Arg<ContactLink>()));
        _contacts.When(c => c.RemoveLinkAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(call => _links.RemoveAll(l => l.Id == call.Arg<Guid>()));

        Services.AddSingleton(new ContactService(_contacts, Substitute.For<ITransactionRepository>(), new Fakes.StoppedClock()));
        Services.AddEleAndUndo();
    }

    [Fact]
    public void Each_pair_has_merge_same_and_different()
    {
        Add("Swiggy");
        Add("SWIGGY LTD");

        var page = RenderComponent<PossibleDuplicates>();

        var pair = page.Find(".duplicate");
        Assert.Equal(["Merge", "Same", "Different"], pair.QuerySelectorAll("button").Select(b => b.TextContent.Trim()));
    }

    [Fact]
    public void The_i_explains_exactly_what_each_button_does()
    {
        Add("Swiggy");
        Add("SWIGGY LTD");

        var page = RenderComponent<PossibleDuplicates>();
        page.Find(".section-head .info-btn").Click();

        var sheet = page.Find("[role=dialog]");
        Assert.Contains("Merge", sheet.TextContent, StringComparison.Ordinal);
        Assert.Contains("Nothing moves", sheet.TextContent, StringComparison.Ordinal);
        Assert.Contains("never suggested again", sheet.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Different_puts_the_pair_away_for_good_and_undo_brings_it_back()
    {
        Add("Swiggy");
        Add("SWIGGY LTD");

        var page = RenderComponent<PossibleDuplicates>();
        page.Find(".duplicate button.not-same").Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".duplicate")));
        Assert.Equal(ContactRelation.NotTheSame, Assert.Single(_links).Relation);
        Assert.Contains("will not be suggested again", Services.GetRequiredService<EleReactions>().Current!.Line, StringComparison.Ordinal);
        Assert.Equal(EleTone.Calm, Services.GetRequiredService<EleReactions>().Current!.Tone);

        page.Find(".section-head button.undo").Click();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".duplicate")));
        Assert.Empty(_links);
    }

    [Fact]
    public void Same_links_the_two_and_keeps_both()
    {
        Add("Swiggy");
        Add("SWIGGY LTD");

        var page = RenderComponent<PossibleDuplicates>();
        page.Find(".duplicate button.same").Click();

        page.WaitForAssertion(() => Assert.Equal(ContactRelation.SameAs, Assert.Single(_links).Relation));
        _contacts.DidNotReceive().MergeAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Merging_asks_which_name_to_keep_and_the_undo_arrow_reverses_it()
    {
        var swiggy = Add("Swiggy");
        var ltd = Add("SWIGGY LTD");
        var merge = new ContactMerge(swiggy.Id, ltd.Id, [], [], [], [], [], [], null, null, 0);
        _contacts.MergeAsync(swiggy.Id, ltd.Id, Arg.Any<CancellationToken>()).Returns(merge);

        var page = RenderComponent<PossibleDuplicates>();
        page.Find(".duplicate button.merge").Click();
        page.FindAll("[role=dialog] .chip").First(c => c.TextContent.Trim() == "Swiggy").Click();
        page.Find("[role=dialog] button.primary").Click();

        page.WaitForAssertion(() => _contacts.Received(1).MergeAsync(swiggy.Id, ltd.Id, Arg.Any<CancellationToken>()));
        Assert.Equal("Merged SWIGGY LTD into Swiggy.", Services.GetRequiredService<EleReactions>().Current!.Line);

        page.Find(".section-head button.undo").Click();

        page.WaitForAssertion(() => _contacts.Received(1).UndoMergeAsync(merge, Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void With_nothing_doubled_the_page_says_so()
    {
        Add("Swiggy");
        Add("Rahul");

        var page = RenderComponent<PossibleDuplicates>();

        Assert.Contains("Nothing looks doubled", page.Markup, StringComparison.Ordinal);
    }

    private ContactSummary Add(string name, int transactions = 1)
    {
        var row = new ContactSummary(Guid.CreateVersion7(), name, transactions, null, 0, false);
        _rows.Add(row);
        _rows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return row;
    }
}
