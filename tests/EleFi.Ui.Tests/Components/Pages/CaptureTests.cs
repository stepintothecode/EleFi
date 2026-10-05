using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Labels;
using EleFi.Application.Transactions;
using EleFi.Application.Typeahead;
using EleFi.Domain.Apps;
using EleFi.Domain.Containers;
using EleFi.Domain.Parties;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>The full capture form: the pickers, the type-to-find fields, and the hand-over from quick capture.</summary>
public class CaptureTests : Bunit.TestContext
{
    private readonly Container _bank = new() { Name = "SBI", Kind = ContainerKind.BankAccount };
    private readonly Container _card = new() { Name = "Amex", Kind = ContainerKind.CreditCard };
    private readonly ITransactionRepository _ledger = Substitute.For<ITransactionRepository>();

    [Fact]
    public void Paid_from_starts_on_the_first_card_and_lists_cards_first()
    {
        Register();

        var page = RenderComponent<Capture>();

        Assert.Equal("Amex", page.Find("#container .picker-name").TextContent);

        page.Find("#container").Click();
        Assert.Equal(["Amex", "SBI"], page.FindAll("[role=option] .picker-name").Select(o => o.TextContent));
    }

    [Fact]
    public void Paid_to_offers_the_most_used_names_and_no_browser_dropdown()
    {
        Register();

        var page = RenderComponent<Capture>();

        // A <datalist> is what drew a popup over the keyboard on Android.
        Assert.Empty(page.FindAll("datalist"));
        Assert.Equal(
            ["Zomato", "Swiggy", "Rahul"],
            page.FindAll(".typeahead-panel [role=option]").Select(o => o.TextContent.Trim()));
    }

    [Fact]
    public void The_app_fields_explain_themselves_behind_an_info_button_not_in_running_text()
    {
        Register();

        var page = RenderComponent<Capture>();

        Assert.NotNull(page.Find(".info-btn[aria-label='What is Bought through?']"));
        Assert.NotNull(page.Find(".info-btn[aria-label='What is Paid with?']"));
        Assert.DoesNotContain("The rail the money moved along", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void What_quick_capture_handed_over_is_already_filled_in()
    {
        Register();
        Services.GetRequiredService<NavigationManager>().NavigateTo($"capture?amount=450&container={_bank.Id}");

        var page = RenderComponent<Capture>();

        Assert.Equal("450", page.Find("#amount").GetAttribute("value"));
        Assert.Equal("SBI", page.Find("#container .picker-name").TextContent);
    }

    [Fact]
    public void Once_paid_to_names_someone_known_the_note_suggests_what_was_written_with_them()
    {
        Register();
        var page = RenderComponent<Capture>();

        Assert.Empty(page.FindAll(".typeahead-panel[aria-label='Notes used before']"));

        page.Find("#party").Input("Rahul");

        page.WaitForAssertion(() =>
        {
            var notes = page.FindAll(".typeahead-panel[aria-label='Notes used before'] [role=option]").Select(o => o.TextContent);
            Assert.Equal(["Rent share", "Dinner"], notes);
        });
    }

    [Fact]
    public void Quick_captures_moment_is_kept_when_it_hands_over()
    {
        Register();
        Services.GetRequiredService<NavigationManager>().NavigateTo("capture?amount=450&date=2026-08-29&time=21:40");

        var page = RenderComponent<Capture>();

        Assert.Equal("2026-08-29", page.Find("#date").GetAttribute("value"));
        Assert.StartsWith("21:40", page.Find("#time").GetAttribute("value"), StringComparison.Ordinal);
    }

    private void Register()
    {
        var now = new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);
        var clock = new Fakes.StoppedClock();
        var labels = new Fakes.SeededLabels();

        var containers = Substitute.For<IContainerRepository>();
        containers.ListSelectableAsync(Arg.Any<CancellationToken>()).Returns([_bank, _card]);

        var parties = Substitute.For<IPartyRepository>();
        parties.ListExternalAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Party { Kind = PartyKind.External, Name = "Rahul", UsageCount = 3, LastUsedAt = now },
            new Party { Kind = PartyKind.External, Name = "Zomato", UsageCount = 9, LastUsedAt = now },
            new Party { Kind = PartyKind.External, Name = "Swiggy", UsageCount = 5, LastUsedAt = now },
            new Party { Kind = PartyKind.External, Name = "Landlord", UsageCount = 1, LastUsedAt = now },
        ]);

        var apps = Substitute.For<IAppRepository>();
        apps.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<App>());

        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton(containers);
        Services.AddSingleton(parties);
        Services.AddSingleton(apps);
        Services.AddSingleton(new LabelService(labels, clock));
        Services.AddSingleton(new CaptureService(Substitute.For<ITransactionRepository>(), parties, apps, labels, clock));
        Services.AddSingleton(new ToastService());
        Services.AddSingleton(new EleFi.Application.Planning.PlanService(Substitute.For<IPlanRepository>(), Substitute.For<ITransactionRepository>(), clock));

        _ledger.NotesForPartyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<TypeaheadCandidate>());
        _ledger.NotesForPartyAsync("Rahul", Arg.Any<CancellationToken>()).Returns(
            [new TypeaheadCandidate("Rent share", 4, now), new TypeaheadCandidate("Dinner", 1, now)]);
        Services.AddSingleton(_ledger);

        var goals = Substitute.For<IGoalRepository>();
        goals.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<EleFi.Domain.Goals.Goal>());
        Services.AddSingleton(new EleFi.Application.Goals.GoalService(goals, _ledger, clock));
    }
}
