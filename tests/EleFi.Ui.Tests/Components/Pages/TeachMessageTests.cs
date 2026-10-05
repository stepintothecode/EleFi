using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Suggestions;
using EleFi.Application.Transactions;
using EleFi.Domain.Alerts;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>Teaching EleFi a message it could not read.</summary>
public class TeachMessageTests : Bunit.TestContext
{
    private const string Unknown = "Spent INR 450.00 on card 4417 at ZOMATO today";
    private readonly List<ParseRule> _rules = [];

    public TeachMessageTests()
    {
        var clock = new Fakes.StoppedClock();

        var suggestions = Substitute.For<ISuggestionRepository>();
        suggestions.ListRulesAsync(Arg.Any<CancellationToken>()).Returns(_ => _rules.ToList());

        var ruleRepository = Substitute.For<IParseRuleRepository>();
        ruleRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => _rules.ToList());
        ruleRepository.When(r => r.AddAsync(Arg.Any<ParseRule>(), Arg.Any<CancellationToken>()))
            .Do(call => _rules.Add(call.Arg<ParseRule>()));

        var containers = new Fakes.EmptyContainers();
        var parties = Substitute.For<IPartyRepository>();
        var apps = Substitute.For<IAppRepository>();
        var transactions = new Fakes.EmptyTransactions();
        var labels = new Fakes.SeededLabels();
        var capture = new CaptureService(transactions, parties, apps, labels, clock);
        var editing = new EditTransactionService(transactions, parties, labels, Substitute.For<IAuditRepository>(), clock);

        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton(new AlertCaptureService(
            suggestions, new AlertPartyResolver(containers, parties, apps, transactions), apps, capture, editing, clock));
        Services.AddSingleton(new ParseRuleService(ruleRepository, clock));
        Services.AddSingleton(new ToastService());
    }

    [Fact]
    public void A_message_eleFi_cannot_read_opens_the_teaching_form()
    {
        var page = Read("VM-AMEXIN", Unknown);

        Assert.Contains("could not read", page.Markup, StringComparison.Ordinal);
        Assert.NotNull(page.Find("#t-amount"));
    }

    [Fact]
    public void Teaching_it_saves_a_rule_and_shows_what_it_now_reads()
    {
        var page = Read("VM-AMEXIN", Unknown);

        page.Find("#t-amount").Change("450.00");
        page.Find("#t-who").Change("ZOMATO");
        page.Find("#t-last4").Change("4417");
        page.FindAll("button.primary")[^1].Click();

        page.WaitForAssertion(() =>
        {
            Assert.Single(_rules);
            Assert.Contains("ZOMATO", page.Find("dl.facts").TextContent, StringComparison.Ordinal);
            Assert.Contains("••4417", page.Find("dl.facts").TextContent, StringComparison.Ordinal);
        });

        // Listed for switching off or forgetting.
        Assert.Contains("What you've taught", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_not_in_the_message_is_explained_and_nothing_is_saved()
    {
        var page = Read("VM-AMEXIN", Unknown);

        page.Find("#t-amount").Change("999.00");
        page.FindAll("button.primary")[^1].Click();

        Assert.Empty(_rules);
        Assert.Contains("999.00", page.Find(".notice.error[role=alert]").TextContent, StringComparison.Ordinal);
    }

    private IRenderedComponent<TeachMessage> Read(string sender, string message)
    {
        var page = RenderComponent<TeachMessage>();
        page.Find("#sender").Change(sender);
        page.Find("#message").Change(message);
        page.Find(".card button.primary").Click();
        return page;
    }
}
