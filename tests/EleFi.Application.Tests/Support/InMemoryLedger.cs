using EleFi.Application.Abstractions;
using EleFi.Domain.Apps;
using EleFi.Domain.Balances;
using EleFi.Domain.Containers;
using EleFi.Domain.Filters;
using EleFi.Domain.Labels;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Tests.Support;

/// <summary>
/// Containers, parties, apps and transactions in lists, wired to each other like the real
/// repositories, for testing services that orchestrate several of them.
/// </summary>
/// <remarks>
/// Query semantics are kept to what the services under test use. The real queries, filters
/// and constraints are tested against a database in EleFi.Infrastructure.Tests.
/// </remarks>
internal sealed class InMemoryLedger
{
    public InMemoryLedger()
    {
        Containers = new ContainerStore(this);
        Parties = new PartyStore(this);
        Apps = new AppStore();
        Transactions = new TransactionStore(this);
        Labels = new LabelStore();
    }

    public List<Container> ContainerRows { get; } = [];

    public List<Party> PartyRows { get; } = [];

    public List<Transaction> TransactionRows { get; } = [];

    public ContainerStore Containers { get; }

    public PartyStore Parties { get; }

    public AppStore Apps { get; }

    public TransactionStore Transactions { get; }

    public LabelStore Labels { get; }

    /// <summary>Adds a container and its Party row.</summary>
    public Container AddContainer(string name, ContainerKind kind, string? last4 = null, string? bank = null)
    {
        var container = new Container { Name = name, Kind = kind, AccountNumberLast4 = last4, InstitutionName = bank };
        ContainerRows.Add(container);
        PartyRows.Add(new Party { Kind = PartyKind.Container, ContainerId = container.Id });
        return container;
    }

    public Party PartyOf(Container container) => PartyRows.Single(p => p.ContainerId == container.Id);

    internal sealed class ContainerStore(InMemoryLedger ledger) : IContainerRepository
    {
        public Task<IReadOnlyList<Container>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Container>>(ledger.ContainerRows);

        public Task<IReadOnlyList<Container>> ListSelectableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Container>>(ledger.ContainerRows.Where(c => !c.IsArchived).ToList());

        public Task<Container?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(ledger.ContainerRows.Find(c => c.Id == id));

        public Task<IReadOnlyList<Container>> FindByLast4Async(string last4, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Container>>(ledger.ContainerRows.Where(c => c.AccountNumberLast4 == last4).ToList());

        public Task AddAsync(Container container, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Container container, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Party?> PartyForAsync(Guid containerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ledger.PartyRows.Find(p => p.ContainerId == containerId));

        public Task<bool> NameTakenAsync(string name, Guid? excluding = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<int> TransactionCountAsync(Guid containerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task SoftDeleteAsync(Guid containerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class PartyStore(InMemoryLedger ledger) : IPartyRepository
    {
        public Task<IReadOnlyList<Party>> SuggestAsync(string? search, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Party>>(ledger.PartyRows.Where(p => p.Kind == PartyKind.External).Take(limit).ToList());

        public Task<IReadOnlyList<Party>> ListExternalAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Party>>(ledger.PartyRows.Where(p => p.Kind == PartyKind.External).ToList());

        public Task<Party?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(ledger.PartyRows.Find(p => p.Id == id));

        public Task<Party> GetOrCreateExternalAsync(string name, CancellationToken cancellationToken = default)
        {
            var existing = ledger.PartyRows.Find(p =>
                p.Kind == PartyKind.External && string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                existing = new Party { Kind = PartyKind.External, Name = name.Trim() };
                ledger.PartyRows.Add(existing);
            }

            return Task.FromResult(existing);
        }

        public Task TouchAsync(IEnumerable<Guid> partyIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class AppStore : IAppRepository
    {
        public List<App> Rows { get; } = [];

        public Task<IReadOnlyList<App>> SuggestAsync(string? search, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<App>>(Rows);

        public Task<IReadOnlyList<App>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<App>>(Rows);

        public Task<App> GetOrCreateAsync(string name, CancellationToken cancellationToken = default)
        {
            var app = Rows.Find(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            if (app is null)
            {
                app = new App { Name = name };
                Rows.Add(app);
            }

            return Task.FromResult(app);
        }

        public Task TouchAsync(IEnumerable<Guid> appIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class LabelStore : ILabelRepository
    {
        public Task<IReadOnlyList<Label>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Label>>([]);

        public Task AddAsync(Label label, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Label?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Label?>(null);

        public Task UpdateAsync(Label label, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> NameTakenAsync(string name, Guid? excluding = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<int> TransactionCountAsync(Guid labelId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IReadOnlyDictionary<Guid, int>> UsageCountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());
    }

    internal sealed class TransactionStore(InMemoryLedger ledger) : ITransactionRepository
    {
        private IEnumerable<Transaction> Live => ledger.TransactionRows.Where(t => t.DeletedAt is null);

        public Task<IReadOnlyList<Transaction>> QueryAsync(
            TransactionFilter filter, int skip = 0, int? take = null, CancellationToken cancellationToken = default)
        {
            var rows = Live
                .Where(t => filter.AppIds.Count == 0 || (t.PaymentAppId is { } p && filter.AppIds.Contains(p)))
                .Where(t => filter.NeedsReview is null || t.NeedsReview == filter.NeedsReview)
                .OrderByDescending(t => t.OccurredOn)
                .ThenByDescending(t => t.CreatedAt)
                .Skip(skip);

            return Task.FromResult<IReadOnlyList<Transaction>>((take is { } n ? rows.Take(n) : rows).Select(Load).ToList());
        }

        public Task<int> CountAsync(TransactionFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(Live.Count());

        public Task<Transaction?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Live.Where(t => t.Id == id).Select(Load).FirstOrDefault());

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            ledger.TransactionRows.Add(Load(transaction));
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            Load(transaction);
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            ledger.TransactionRows.Find(t => t.Id == id)!.DeletedAt = DateTimeOffset.UnixEpoch;
            return Task.CompletedTask;
        }

        public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
        {
            ledger.TransactionRows.Find(t => t.Id == id)!.DeletedAt = null;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ContainerBalance>> BalancesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContainerBalance>>([]);

        public Task<SpendBreakdown> SpendByLabelAsync(TransactionFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpendBreakdown([], 0));

        public Task<FlowTotals> TotalsAsync(TransactionFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(default(FlowTotals));

        public Task<IReadOnlyList<Transaction>> ListDeletedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>(ledger.TransactionRows.Where(t => t.DeletedAt is not null).ToList());

        public Task<IReadOnlyList<EleFi.Application.Typeahead.TypeaheadCandidate>> NotesForPartyAsync(string partyName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EleFi.Application.Typeahead.TypeaheadCandidate>>(ledger.TransactionRows
                .Where(t => t.DeletedAt is null && !string.IsNullOrWhiteSpace(t.Description)
                    && (string.Equals(ledger.PartyRows.FirstOrDefault(p => p.Id == t.DestinationPartyId)?.Name, partyName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(ledger.PartyRows.FirstOrDefault(p => p.Id == t.SourcePartyId)?.Name, partyName, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(t => t.Description!, StringComparer.OrdinalIgnoreCase)
                .Select(g => new EleFi.Application.Typeahead.TypeaheadCandidate(g.Key, g.Count(), g.Max(t => t.CreatedAt)))
                .ToList());

        // The real repository loads the parties and apps the derived Kind and the UI need.
        private Transaction Load(Transaction t)
        {
            t.SourceParty = ledger.PartyRows.Find(p => p.Id == t.SourcePartyId);
            t.DestinationParty = ledger.PartyRows.Find(p => p.Id == t.DestinationPartyId);
            t.PaymentApp = ledger.Apps.Rows.Find(a => a.Id == t.PaymentAppId);
            return t;
        }
    }
}
