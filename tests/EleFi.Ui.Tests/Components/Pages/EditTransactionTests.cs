using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Goals;
using EleFi.Application.Labels;
using EleFi.Application.Transactions;
using EleFi.Domain.Apps;
using EleFi.Domain.Audit;
using EleFi.Domain.Containers;
using EleFi.Domain.Goals;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>
/// The editor: its history reads in names, a save says the transaction was updated, and Ele
/// reacts to what happened.
/// </summary>
public class EditTransactionTests : Bunit.TestContext
{
    private readonly Container _card = new() { Name = "Amex", Kind = ContainerKind.CreditCard };
    private readonly Party _cardParty;
    private readonly Party _zomato = new() { Kind = PartyKind.External, Name = "Zomato" };
    private readonly Party _swiggy = new() { Kind = PartyKind.External, Name = "Swiggy" };
    private readonly Transaction _lunch;
    private readonly ITransactionRepository _ledger = Substitute.For<ITransactionRepository>();
    private readonly EleReactions _ele = new(new Fakes.MemorySettings(), new ManualTime());

    public EditTransactionTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _cardParty = new Party { Kind = PartyKind.Container, ContainerId = _card.Id };
        _lunch = new Transaction
        {
            SourcePartyId = _cardParty.Id,
            SourceParty = _cardParty,
            DestinationPartyId = _swiggy.Id,
            DestinationParty = _swiggy,
            SourceAmountMinor = 45000,
            DestinationAmountMinor = 45000,
            OccurredOn = new DateOnly(2026, 8, 30),
            NeedsReview = true,
        };

        _ledger.FindAsync(_lunch.Id, Arg.Any<CancellationToken>()).Returns(_ => _lunch.DeletedAt is null ? _lunch : null);
        _ledger.NotesForPartyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _ledger.When(l => l.SoftDeleteAsync(_lunch.Id, Arg.Any<CancellationToken>()))
            .Do(_ => _lunch.DeletedAt = DateTimeOffset.UnixEpoch);

        var parties = Substitute.For<IPartyRepository>();
        parties.ListExternalAsync(Arg.Any<CancellationToken>()).Returns([_zomato, _swiggy]);
        parties.GetOrCreateExternalAsync("Swiggy", Arg.Any<CancellationToken>()).Returns(_swiggy);
        foreach (var party in new[] { _cardParty, _zomato, _swiggy })
        {
            parties.FindAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);
        }

        parties.NamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(
            new Dictionary<Guid, string> { [_zomato.Id] = "Zomato", [_swiggy.Id] = "Swiggy", [_cardParty.Id] = "Amex" });

        var apps = Substitute.For<IAppRepository>();
        apps.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<App>());
        apps.NamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, string>());

        var containers = Substitute.For<IContainerRepository>();
        containers.ListSelectableAsync(Arg.Any<CancellationToken>()).Returns([_card]);
        containers.PartyForAsync(_card.Id, Arg.Any<CancellationToken>()).Returns(_cardParty);

        // The payee was corrected once, from Zomato to Swiggy.
        var audit = Substitute.For<IAuditRepository>();
        audit.TimelineAsync(EditTransactionService.EntityType, _lunch.Id, Arg.Any<CancellationToken>()).Returns(
        [
            new AuditEvent
            {
                Action = AuditAction.Updated,
                OccurredAt = DateTimeOffset.UnixEpoch,
                Changes = $"[{{\"field\":\"DestinationPartyId\",\"from\":\"{_zomato.Id}\",\"to\":\"{_swiggy.Id}\"}}]",
            },
        ]);

        var goals = Substitute.For<IGoalRepository>();
        goals.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Goal>());

        var clock = new Fakes.StoppedClock();
        var labels = new Fakes.SeededLabels();

        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton(new EditTransactionService(_ledger, parties, labels, audit, clock, apps));
        Services.AddSingleton(_ledger);
        Services.AddSingleton(new GoalService(goals, _ledger, clock));
        Services.AddSingleton(containers);
        Services.AddSingleton(new LabelService(labels, clock));
        Services.AddSingleton(parties);
        Services.AddSingleton(apps);
        Services.AddSingleton(_ele);
        Services.AddEleAndUndo();
    }

    [Fact]
    public void The_history_names_who_was_paid_instead_of_showing_an_id()
    {
        var page = Render();

        var history = page.Find(".timeline-changes").TextContent;
        Assert.Contains("Paid to", history, StringComparison.Ordinal);
        Assert.Contains("Zomato", history, StringComparison.Ordinal);
        Assert.Contains("Swiggy", history, StringComparison.Ordinal);
        Assert.DoesNotContain(_zomato.Id.ToString(), history, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Who_it_was_paid_to_links_to_their_contact()
    {
        var page = Render();

        Assert.Equal($"contacts/{_swiggy.Id}", page.Find("a.contact-link").GetAttribute("href"));
    }

    [Fact]
    public void Saving_brings_ele_on_and_lights_the_undo_arrow_beside_close()
    {
        var page = Render();
        Assert.True(page.Find(".section-head button.undo").HasAttribute("disabled"));

        page.FindAll("button").First(b => b.TextContent.Trim() == "Save changes").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Equal(EleMoment.Updated, _ele.Current!.Moment);
            Assert.False(page.Find(".section-head button.undo").HasAttribute("disabled"));
        });

        // There is no toast any more: Ele is the only thing that speaks.
        Assert.Empty(page.FindAll(".toast"));
    }

    [Fact]
    public void An_empty_amount_is_a_problem_ele_says_crossly()
    {
        var page = Render();

        page.Find("#amount").Change(string.Empty);
        page.FindAll("button").First(b => b.TextContent.Trim() == "Save changes").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Equal(EleTone.Cross, _ele.Current!.Tone);
            Assert.Equal("Enter an amount greater than zero.", _ele.Current.Line);
        });
    }

    [Fact]
    public void Looks_right_brings_ele_on_pleased()
    {
        var page = Render();

        page.Find(".review-bar button.primary").Click();

        page.WaitForAssertion(() => Assert.Equal(EleMoment.Reviewed, _ele.Current!.Moment));
    }

    [Fact]
    public void Deleting_brings_ele_on_calm_and_can_be_undone()
    {
        var page = Render();

        page.FindAll("button").First(b => b.TextContent.Trim() == "Delete this transaction").Click();
        page.FindAll("button").First(b => b.TextContent.Trim() == "Delete").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Equal(EleMoment.Deleted, _ele.Current!.Moment);

            // Calm, not cross and not pleased: a deletion is neither a mistake nor a win.
            Assert.Equal(EleTone.Calm, _ele.Current.Tone);
        });

        Assert.True(Services.GetRequiredService<UndoHistory>().CanUndo(UndoHistory.Transactions));
    }

    private IRenderedComponent<EditTransaction> Render()
    {
        var page = RenderComponent<EditTransaction>(p => p.Add(e => e.Id, _lunch.Id));
        page.WaitForAssertion(() => Assert.NotNull(page.Find(".timeline-entry")));
        return page;
    }
}
