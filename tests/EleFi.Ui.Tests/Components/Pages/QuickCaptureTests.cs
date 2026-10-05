using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>
/// Quick capture: three steps, then Done or Add other details.
/// </summary>
public class QuickCaptureTests : Bunit.TestContext
{
    private readonly Container _card = new() { Name = "Amex", Kind = ContainerKind.CreditCard };
    private readonly Container _bank = new() { Name = "SBI", Kind = ContainerKind.BankAccount };
    private readonly Party _cardParty = new() { Kind = PartyKind.Container };
    private readonly Party _zomato = new() { Kind = PartyKind.External, Name = "Zomato", UsageCount = 4 };
    private readonly RecordingTransactions _transactions = new();

    [Fact]
    public void From_lists_cards_first_with_no_headings()
    {
        Register();
        var page = RenderComponent<QuickCapture>();

        page.Find("#q-amount").Input("450");
        page.Find(".quick-actions button.primary").Click();

        Assert.Equal(["Amex", "SBI"], page.FindAll(".container-choices button > span").Select(s => s.TextContent));
    }

    [Fact]
    public void To_offers_the_most_used_names_above_the_field()
    {
        Register();
        var page = RenderComponent<QuickCapture>();

        page.Find("#q-amount").Input("450");
        page.Find(".quick-actions button.primary").Click();
        page.Find(".container-choices button").Click();

        Assert.Equal("Zomato", page.Find("[role=option]").TextContent.Trim());
    }

    [Fact]
    public void Add_other_details_is_not_offered_until_the_last_step()
    {
        Register();
        var page = RenderComponent<QuickCapture>();

        page.Find("#q-amount").Input("450");
        Assert.Empty(page.FindAll(".quick-more"));

        page.Find(".quick-actions button.primary").Click();
        Assert.Empty(page.FindAll(".quick-more"));
    }

    [Fact]
    public void After_the_last_step_done_records_it_flagged_for_review()
    {
        Register();
        var page = ToTheLastQuestion();

        page.Find(".quick-done-button").Click();

        var saved = Assert.Single(_transactions.Added);
        Assert.True(saved.NeedsReview);
        Assert.Equal(45000, saved.SourceAmountMinor);
        Assert.Equal(new Fakes.StoppedClock().Today, saved.OccurredOn);
    }

    [Fact]
    public void Add_other_details_opens_the_full_form_filled_in_and_saves_nothing()
    {
        Register();
        var page = ToTheLastQuestion();

        page.Find(".quick-more").Click();

        // Nothing is recorded until the full form is saved.
        Assert.Empty(_transactions.Added);

        var uri = Services.GetRequiredService<NavigationManager>().Uri;
        Assert.Contains("capture?amount=450", uri, StringComparison.Ordinal);
        Assert.Contains($"container={_card.Id}", uri, StringComparison.Ordinal);
        Assert.Contains("party=Zomato", uri, StringComparison.Ordinal);
        Assert.Contains($"date={new Fakes.StoppedClock().Today:yyyy-MM-dd}", uri, StringComparison.Ordinal);
        Assert.Contains("time=", uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Tapping_a_suggested_name_asks_done_rather_than_recording_straight_away()
    {
        Register();
        var page = RenderComponent<QuickCapture>();

        page.Find("#q-amount").Input("450");
        page.Find(".quick-actions button.primary").Click();
        page.Find(".container-choices button").Click();
        page.Find("[role=option]").Click();

        Assert.Empty(_transactions.Added);
        Assert.Contains("Zomato", page.Find(".quick-summary").TextContent, StringComparison.Ordinal);
    }

    private IRenderedComponent<QuickCapture> ToTheLastQuestion()
    {
        var page = RenderComponent<QuickCapture>();

        page.Find("#q-amount").Input("450");
        page.Find(".quick-actions button.primary").Click();
        page.Find(".container-choices button").Click();
        page.Find("#q-to").Input("Zomato");
        page.Find(".quick-actions button.primary").Click();

        return page;
    }

    private void Register()
    {
        var clock = new Fakes.StoppedClock();

        var containers = Substitute.For<IContainerRepository>();
        containers.ListSelectableAsync(Arg.Any<CancellationToken>()).Returns([_bank, _card]);
        containers.PartyForAsync(_card.Id, Arg.Any<CancellationToken>()).Returns(_cardParty);

        var parties = Substitute.For<IPartyRepository>();
        parties.ListExternalAsync(Arg.Any<CancellationToken>()).Returns([_zomato]);
        parties.GetOrCreateExternalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_zomato);
        parties.FindAsync(_cardParty.Id, Arg.Any<CancellationToken>()).Returns(_cardParty);
        parties.FindAsync(_zomato.Id, Arg.Any<CancellationToken>()).Returns(_zomato);

        var apps = Substitute.For<IAppRepository>();
        var labels = new Fakes.SeededLabels();

        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton(containers);
        Services.AddSingleton(parties);
        Services.AddSingleton<IMascotService>(new Fakes.SilentMascot());
        Services.AddSingleton(new CaptureService(_transactions, parties, apps, labels, clock));
    }

    private sealed class RecordingTransactions : ITransactionRepository
    {
        private readonly Fakes.EmptyTransactions _empty = new();

        public List<Transaction> Added { get; } = [];

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            Added.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Transaction>> QueryAsync(
            Domain.Filters.TransactionFilter filter, int skip = 0, int? take = null, CancellationToken cancellationToken = default) =>
            _empty.QueryAsync(filter, skip, take, cancellationToken);

        public Task<int> CountAsync(Domain.Filters.TransactionFilter filter, CancellationToken cancellationToken = default) =>
            _empty.CountAsync(filter, cancellationToken);

        public Task<Transaction?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            _empty.FindAsync(id, cancellationToken);

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Domain.Balances.ContainerBalance>> BalancesAsync(CancellationToken cancellationToken = default) =>
            _empty.BalancesAsync(cancellationToken);

        public Task<SpendBreakdown> SpendByLabelAsync(
            Domain.Filters.TransactionFilter filter, CancellationToken cancellationToken = default) =>
            _empty.SpendByLabelAsync(filter, cancellationToken);

        public Task<FlowTotals> TotalsAsync(Domain.Filters.TransactionFilter filter, CancellationToken cancellationToken = default) =>
            _empty.TotalsAsync(filter, cancellationToken);

        public Task<IReadOnlyList<Transaction>> ListDeletedAsync(CancellationToken cancellationToken = default) =>
            _empty.ListDeletedAsync(cancellationToken);

        public Task<IReadOnlyList<EleFi.Application.Typeahead.TypeaheadCandidate>> NotesForPartyAsync(string partyName, CancellationToken cancellationToken = default) =>
            _empty.NotesForPartyAsync(partyName, cancellationToken);
    }
}
