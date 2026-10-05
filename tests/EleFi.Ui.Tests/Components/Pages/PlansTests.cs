using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Planning;
using EleFi.Domain.Containers;
using EleFi.Domain.Parties;
using EleFi.Domain.Planning;
using EleFi.Domain.Transactions;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>The Plans page: a to-do list with money in it.</summary>
public class PlansTests : Bunit.TestContext
{
    private readonly List<Plan> _plans = [];
    private readonly ITransactionRepository _ledger = Substitute.For<ITransactionRepository>();
    private readonly Fakes.StoppedClock _clock = new();
    private readonly Container _sbi = new() { Name = "SBI", Kind = ContainerKind.BankAccount };

    public PlansTests()
    {
        var repository = Substitute.For<IPlanRepository>();
        repository.ListOpenAsync(Arg.Any<CancellationToken>()).Returns(_ => _plans.Where(p => !p.IsDone && p.DeletedAt is null).OrderBy(p => p.DueOn).ToList());
        repository.ListCompletedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(_ => _plans.Where(p => p.IsDone).ToList());
        repository.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => _plans.Find(p => p.Id == call.Arg<Guid>()));
        repository.When(r => r.AddAsync(Arg.Any<Plan>(), Arg.Any<CancellationToken>())).Do(call => _plans.Add(call.Arg<Plan>()));
        repository.ClaimedTransactionIdsAsync(Arg.Any<CancellationToken>()).Returns(new HashSet<Guid>());

        var containers = Substitute.For<IContainerRepository>();
        containers.ListSelectableAsync(Arg.Any<CancellationToken>()).Returns([_sbi]);
        var parties = Substitute.For<IPartyRepository>();
        parties.ListExternalAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Party>());

        Services.AddSingleton<IClock>(_clock);
        Services.AddSingleton(new PlanService(repository, _ledger, _clock));
        Services.AddSingleton(containers);
        Services.AddSingleton(parties);
        Services.AddSingleton(new ToastService());
    }

    [Fact]
    public void Plans_are_grouped_by_when_they_are_due()
    {
        _plans.Add(new Plan { Title = "Card bill", DueOn = _clock.Today.AddDays(-2) });
        _plans.Add(new Plan { Title = "Rent", DueOn = _clock.Today });
        _plans.Add(new Plan { Title = "Insurance", DueOn = _clock.Today.AddDays(20) });

        var page = RenderComponent<Plans>();

        Assert.Equal(["Overdue", "Today", "Later"], page.FindAll("h2.plan-group").Select(h => h.TextContent));
    }

    [Fact]
    public void A_new_plan_is_made_in_a_sheet_and_lands_on_the_list()
    {
        var page = RenderComponent<Plans>();

        page.Find(".section-head button.primary").Click();
        page.Find("#plan-title").Change("SIP, Axis Bluechip");
        page.Find("#plan-amount").Change("5000");
        page.Find("#plan-repeat").Change(RepeatFrequency.Monthly.ToString());
        page.Find("[role=dialog] button.primary").Click();

        var plan = Assert.Single(_plans);
        Assert.Equal(500000, plan.AmountMinor);
        Assert.Equal(RepeatFrequency.Monthly, plan.RepeatFrequency);
        Assert.Contains("SIP, Axis Bluechip", page.Find(".plan-list").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Ticking_a_plan_with_no_amount_marks_it_done_and_moves_it_to_completed()
    {
        _plans.Add(new Plan { Title = "Call the bank", DueOn = _clock.Today });
        var page = RenderComponent<Plans>();

        page.Find("button[aria-label='Mark Call the bank done']").Click();

        Assert.True(_plans[0].IsDone);
        Assert.Contains("Completed (1)", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Ticking_a_plan_nothing_matches_offers_to_record_it_through_the_capture_form()
    {
        _plans.Add(new Plan { Title = "SIP", AmountMinor = 500000, ContainerId = _sbi.Id, DueOn = _clock.Today });
        var page = RenderComponent<Plans>();

        page.Find("button[aria-label='Mark SIP done']").Click();
        page.FindAll("[role=dialog] button.primary").First(b => b.TextContent.Contains("Record", StringComparison.Ordinal)).Click();

        var uri = Services.GetRequiredService<NavigationManager>().Uri;
        Assert.Contains("capture?plan=", uri, StringComparison.Ordinal);
        Assert.Contains("amount=5000", uri, StringComparison.Ordinal);
        Assert.Contains($"container={_sbi.Id}", uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Ticking_a_plan_with_exactly_one_matching_transaction_links_it_straight_away()
    {
        var sip = new Plan { Title = "SIP", AmountMinor = 500000, DueOn = _clock.Today };
        _plans.Add(sip);
        var mine = new Party { Kind = PartyKind.Container, ContainerId = _sbi.Id };
        var fund = new Party { Kind = PartyKind.External, Name = "Fund" };
        var paid = new Transaction
        {
            SourceParty = mine, SourcePartyId = mine.Id, DestinationParty = fund, DestinationPartyId = fund.Id,
            SourceAmountMinor = 500000, DestinationAmountMinor = 500000, OccurredOn = _clock.Today,
        };
        _ledger.QueryAsync(Arg.Any<Domain.Filters.TransactionFilter>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([paid]);
        var page = RenderComponent<Plans>();

        page.Find("button[aria-label='Mark SIP done']").Click();

        Assert.Equal(paid.Id, sip.TransactionId);
        Assert.Empty(page.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Goals_have_a_place_on_the_same_page()
    {
        var page = RenderComponent<Plans>();

        page.FindAll(".segmented button")[1].Click();

        Assert.Contains("Goals are coming", page.Markup, StringComparison.Ordinal);
    }
}
