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
/// Quick capture: three steps, and a way out to the full form that keeps what was typed.
/// </summary>
public class QuickCaptureTests : Bunit.TestContext
{
    private readonly Container _card = new() { Name = "Amex", Kind = ContainerKind.CreditCard };
    private readonly Container _bank = new() { Name = "SBI", Kind = ContainerKind.BankAccount };
    private readonly Party _cardParty = new() { Kind = PartyKind.Container };
    private readonly Party _zomato = new() { Kind = PartyKind.External, Name = "Zomato", UsageCount = 4 };
    private readonly RecordingTransactions _transactions = new();

    [Fact]
    public void From_is_sectioned_cards_first()
    {
        Register();
        var page = RenderComponent<QuickCapture>();

        page.Find("#q-amount").Input("450");
        page.Find(".quick-actions button.primary").Click();

        Assert.Equal(
            ["Credit cards", "Bank accounts"],
            page.FindAll(".choice-heading").Select(h => h.TextContent));
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
    public void Add_other_details_with_no_counterparty_hands_the_amount_to_the_full_form()
    {
        Register();
        var page = RenderComponent<QuickCapture>();

        page.Find("#q-amount").Input("450");
        page.Find(".quick-more").Click();

        // Nothing could honestly be saved without a counterparty, so nothing was.
        Assert.Empty(_transactions.Added);

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("capture?amount=450", nav.Uri, StringComparison.Ordinal);
        Assert.Contains($"container={_card.Id}", nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_other_details_with_both_ends_saves_and_opens_the_editor_on_it()
    {
        Register();
        var page = RenderComponent<QuickCapture>();

        page.Find("#q-amount").Input("450");
        page.Find(".quick-actions button.primary").Click();
        page.Find(".container-choices button").Click();
        page.Find("#q-to").Input("Zomato");
        page.Find(".quick-more").Click();

        var saved = Assert.Single(_transactions.Added);

        // Still a quick capture: flagged so the editor's review banner explains itself.
        Assert.True(saved.NeedsReview);
        Assert.Equal(45000, saved.SourceAmountMinor);

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/transactions/{saved.Id}", nav.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_other_details_waits_for_an_amount()
    {
        Register();
        var page = RenderComponent<QuickCapture>();

        Assert.True(page.Find(".quick-more").HasAttribute("disabled"));
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
    }
}
