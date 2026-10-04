using EleFi.Application.Abstractions;
using EleFi.Domain.Balances;
using EleFi.Domain.Containers;
using EleFi.Domain.Filters;
using EleFi.Domain.Labels;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;

namespace EleFi.Ui.Tests.Support;

/// <summary>
/// Empty implementations of the ports a page needs to render.
/// </summary>
/// <remarks>
/// Shared rather than copied per test class. The same three fakes had been written out in
/// three files, so every method added to a repository interface broke all three and got
/// patched three times. One copy means an interface change is felt once.
/// </remarks>
internal static class Fakes
{
    /// <summary>A clock that does not move, so no test depends on when it ran.</summary>
    public sealed class StoppedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

        public DateOnly Today { get; } = new(2026, 8, 30);

        public TimeOnly TimeOfDay { get; } = new(9, 0);
    }

    /// <summary>A mascot that does nothing, quietly.</summary>
    public sealed class SilentMascot : IMascotService
    {
        public bool IsReady => false;

        public Task InitialiseAsync(string canvasElementId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetMoodAsync(MascotMood mood, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task CheerAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ConcernAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A launch that came from the launcher icon, or one that asked for a screen.</summary>
    public sealed class PlainLaunch(string? route = null) : ILaunchIntent
    {
        private string? _route = route;

        public event Action? RouteRequested;

        public string? ConsumeRequestedRoute()
        {
            var route = _route;
            _route = null;
            return route;
        }

        /// <summary>Simulates a request arriving while the app is already open.</summary>
        public void Request(string route)
        {
            _route = route;
            RouteRequested?.Invoke();
        }
    }

    /// <summary>A back control that records whether the app was asked to leave.</summary>
    public sealed class RecordingBack : ISystemBack
    {
        public event Action? Pressed;

        public int Left { get; private set; }

        public void LeaveApp() => Left++;

        /// <summary>Simulates the user pressing back.</summary>
        public void Press() => Pressed?.Invoke();
    }

    /// <summary>A device with no containers on it.</summary>
    public sealed class EmptyContainers : IContainerRepository
    {
        public Task<IReadOnlyList<Container>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Container>>([]);

        public Task<IReadOnlyList<Container>> ListSelectableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Container>>([]);

        public Task<Container?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Container?>(null);

        public Task<IReadOnlyList<Container>> FindByLast4Async(
            string last4, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Container>>([]);

        public Task AddAsync(Container container, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(Container container, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Party?> PartyForAsync(Guid containerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Party?>(null);

        public Task<bool> NameTakenAsync(
            string name, Guid? excluding = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<int> TransactionCountAsync(Guid containerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task SoftDeleteAsync(Guid containerId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    /// <summary>A ledger with nothing in it, apart from whatever breakdown a test sets.</summary>
    public sealed class EmptyTransactions : ITransactionRepository
    {
        /// <summary>What <see cref="SpendByLabelAsync"/> returns. Empty unless a test says otherwise.</summary>
        public SpendBreakdown Spend { get; set; } = new([], 0);

        public Task<IReadOnlyList<Transaction>> QueryAsync(
            TransactionFilter filter, int skip = 0, int? take = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>([]);

        public Task<int> CountAsync(TransactionFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<Transaction?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Transaction?>(null);

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ContainerBalance>> BalancesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContainerBalance>>([]);

        public Task<SpendBreakdown> SpendByLabelAsync(
            DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(Spend);
    }

    /// <summary>A small starter label set.</summary>
    public sealed class SeededLabels : ILabelRepository
    {
        private readonly List<Label> _labels =
        [
            new() { Name = "Food", Colour = "#f97316" },
            new() { Name = "Travel", Colour = "#0ea5e9" },
        ];

        public Task<IReadOnlyList<Label>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Label>>(_labels);

        public Task AddAsync(Label label, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Label?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_labels.Find(l => l.Id == id));

        public Task UpdateAsync(Label label, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> NameTakenAsync(
            string name, Guid? excluding = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<int> TransactionCountAsync(Guid labelId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyDictionary<Guid, int>> UsageCountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());
    }
}
