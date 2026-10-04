using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Labels;
using EleFi.Application.Suggestions;
using EleFi.Application.Transactions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Containers;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>The inbox of payments spotted but not yet confirmed.</summary>
public class SuggestionsTests : Bunit.TestContext
{
    private readonly Container _card = new() { Name = "HDFC Card", Kind = ContainerKind.CreditCard };
    private readonly ISuggestionRepository _repository = Substitute.For<ISuggestionRepository>();
    private readonly ISuggestionPromptSurface _prompts = Substitute.For<ISuggestionPromptSurface>();

    [Fact]
    public void A_merged_suggestion_shows_both_sources_the_payee_and_the_note()
    {
        var suggestion = Merged();
        Register(suggestion);

        var page = RenderComponent<Suggestions>();

        var sources = page.Find(".evidence").TextContent;
        Assert.Contains("Bank SMS", sources, StringComparison.Ordinal);
        Assert.Contains("GPay", sources, StringComparison.Ordinal);
        Assert.Contains("Lunch", page.Markup, StringComparison.Ordinal);
        Assert.Equal("Zomato", page.Find($"#who-{suggestion.Id}").GetAttribute("value"));
    }

    [Fact]
    public void Containers_are_offered_cards_first_under_headings()
    {
        Register(Merged());

        var page = RenderComponent<Suggestions>();

        Assert.Equal("Credit cards", page.Find("optgroup").GetAttribute("label"));
    }

    [Fact]
    public void SM11_an_unmatched_suggestion_asks_for_a_container_rather_than_guessing_one()
    {
        var suggestion = Merged();
        suggestion.ContainerId = null;
        Register(suggestion);

        var page = RenderComponent<Suggestions>();
        page.Find(".card button.primary").Click();

        Assert.Contains("Choose one", page.Markup, StringComparison.Ordinal);
        Assert.Contains(Services.GetRequiredService<ToastService>().Current, t => t.Message.Contains("Choose which container", StringComparison.Ordinal));
    }

    [Fact]
    public void Dismissing_destroys_it_and_takes_its_notification_away()
    {
        var suggestion = Merged();
        Register(suggestion);

        var page = RenderComponent<Suggestions>();
        page.Find(".card .row button.btn-ghost").Click();

        _repository.Received().DeleteAsync(suggestion.Id, Arg.Any<CancellationToken>());
        _prompts.Received().Withdraw(suggestion.Id);
    }

    [Fact]
    public void D1_a_card_bill_asks_which_card_was_paid_and_offers_no_labels()
    {
        var bill = Merged();
        bill.Direction = TransactionKind.SelfTransfer;
        bill.CounterpartyText = "HDFC Credit Card";
        Register(bill);

        var page = RenderComponent<Suggestions>();

        Assert.Contains("card bill, not spending", page.Markup, StringComparison.Ordinal);
        Assert.NotNull(page.Find($"#card-{bill.Id}"));
        Assert.Empty(page.FindAll($"#who-{bill.Id}"));
        Assert.Empty(page.FindAll(".label-picker"));
    }

    [Fact]
    public void A_merge_that_lands_while_the_inbox_is_open_updates_the_card()
    {
        var suggestion = Merged();
        suggestion.CounterpartyText = "VPA zomato@hdfcbank";
        suggestion.ContainerId = null;
        Register(suggestion);

        var page = RenderComponent<Suggestions>();
        Assert.Equal("VPA zomato@hdfcbank", page.Find($"#who-{suggestion.Id}").GetAttribute("value"));

        // The app's notification is merged in, and the prompt's Review is tapped.
        suggestion.CounterpartyText = "Zomato";
        suggestion.ContainerId = _card.Id;
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo("suggestions");

        page.WaitForAssertion(() =>
        {
            Assert.Equal("Zomato", page.Find($"#who-{suggestion.Id}").GetAttribute("value"));
            Assert.Equal(_card.Id.ToString(), page.Find($"#from-{suggestion.Id}").GetAttribute("value"));
        });
    }

    [Fact]
    public void With_nothing_waiting_it_says_so()
    {
        Register();

        var page = RenderComponent<Suggestions>();

        Assert.Contains("Nothing waiting", page.Markup, StringComparison.Ordinal);
    }

    private CaptureSuggestion Merged() => new()
    {
        AmountMinor = 45000,
        CurrencyCode = "INR",
        Direction = TransactionKind.Debit,
        CounterpartyText = "Zomato",
        Note = "Lunch",
        PaymentAppName = "GPay",
        Evidence = AlertEvidence.Sms | AlertEvidence.PaymentApp,
        ContainerId = _card.Id,
        OccurredOn = new DateOnly(2026, 8, 30),
    };

    private void Register(params CaptureSuggestion[] pending)
    {
        var clock = new Fakes.StoppedClock();
        _repository.ListPendingAsync(Arg.Any<CancellationToken>()).Returns(pending);

        var containers = Substitute.For<IContainerRepository>();
        containers.ListSelectableAsync(Arg.Any<CancellationToken>()).Returns([_card]);

        var parties = Substitute.For<IPartyRepository>();
        parties.ListExternalAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Party>());

        var apps = Substitute.For<IAppRepository>();
        var labels = new Fakes.SeededLabels();
        var capture = new CaptureService(Substitute.For<ITransactionRepository>(), parties, apps, labels, clock);

        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton(containers);
        Services.AddSingleton(parties);
        Services.AddSingleton(_prompts);
        Services.AddSingleton(new LabelService(labels, clock));
        Services.AddSingleton(new SuggestionService(_repository, containers, parties, apps, capture, clock));
        Services.AddSingleton(new ToastService());
    }
}
