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

    /// <summary>Small settings in a dictionary.</summary>
    public sealed class MemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = [];

        public string Read(string key, string fallback) => _values.GetValueOrDefault(key, fallback);

        public void Write(string key, string value) => _values[key] = value;
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

    /// <summary>Ele's sound, recorded rather than played.</summary>
    public sealed class RecordingSound : ISoundCue
    {
        public List<SoundCue> Played { get; } = [];

        public Task PlayAsync(SoundCue cue, CancellationToken cancellationToken = default)
        {
            Played.Add(cue);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>The app coming back to the front, when the test says so.</summary>
    public sealed class ManualResume : IAppResume
    {
        public event Action? Resumed;

        /// <summary>Simulates the app coming back from the background.</summary>
        public void Resume() => Resumed?.Invoke();
    }

    /// <summary>A phone contacts picker that hands back whatever the test says the user picked.</summary>
    public sealed class FakeContactPicker : IContactPicker
    {
        public bool IsSupported { get; init; } = true;

        /// <summary>What the next pick returns: null is the user backing out.</summary>
        public PickedContact? Next { get; set; }

        public List<string> Opened { get; } = [];

        public Task<PickedContact?> PickAsync(CancellationToken cancellationToken = default) => Task.FromResult(Next);

        public void Open(string lookupKey) => Opened.Add(lookupKey);
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

        public Task RestoreAsync(Guid containerId, CancellationToken cancellationToken = default) =>
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

        /// <summary>What <see cref="CountAsync"/> returns for a needs-review filter.</summary>
        public int NeedsReviewCount { get; set; }

        /// <summary>What <see cref="BalancesAsync"/> returns.</summary>
        public IReadOnlyList<ContainerBalance> Balances { get; set; } = [];

        public Task<int> CountAsync(TransactionFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(filter.NeedsReview == true ? NeedsReviewCount : 0);

        public Task<Transaction?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Transaction?>(null);

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ContainerBalance>> BalancesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Balances);

        /// <summary>The filter the dashboard last asked about, so a test can check its range.</summary>
        public TransactionFilter? LastSpendFilter { get; private set; }

        /// <summary>What <see cref="TotalsAsync"/> returns.</summary>
        public FlowTotals Totals { get; set; }

        public Task<SpendBreakdown> SpendByLabelAsync(
            TransactionFilter filter, CancellationToken cancellationToken = default)
        {
            LastSpendFilter = filter;
            return Task.FromResult(Spend);
        }

        public Task<FlowTotals> TotalsAsync(TransactionFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(Totals);

        public Task<IReadOnlyList<Transaction>> ListDeletedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>([]);

        public Task<IReadOnlyList<EleFi.Application.Typeahead.TypeaheadCandidate>> NotesForPartyAsync(string partyName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EleFi.Application.Typeahead.TypeaheadCandidate>>([]);

        /// <summary>What <see cref="ReviewFactsAsync"/> returns.</summary>
        public ReviewFacts Facts { get; set; } = new(null, []);

        public Task<ReviewFacts> ReviewFactsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Facts);
    }

    /// <summary>A small starter label set.</summary>
    public sealed class SeededLabels : ILabelRepository
    {
        private readonly List<Label> _labels =
        [
            new() { Name = "Food", Colour = "#f97316" },
            new() { Name = "Travel", Colour = "#0ea5e9" },
        ];

        private readonly List<Label> _deleted = [];

        public Task<IReadOnlyList<Label>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Label>>(_labels.ToList());

        public Task AddAsync(Label label, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Label?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_labels.Find(l => l.Id == id));

        public Task UpdateAsync(Label label, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _deleted.AddRange(_labels.Where(l => l.Id == id));
            _labels.RemoveAll(l => l.Id == id);
            return Task.FromResult<IReadOnlyList<Guid>>([]);
        }

        public Task RestoreAsync(Guid id, IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default)
        {
            _labels.AddRange(_deleted.Where(l => l.Id == id));
            _deleted.RemoveAll(l => l.Id == id);
            return Task.CompletedTask;
        }

        public Task<bool> NameTakenAsync(
            string name, Guid? excluding = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<int> TransactionCountAsync(Guid labelId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyDictionary<Guid, int>> UsageCountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());
    }

    /// <summary>Receipts and photos in lists, so receipt screens render and attach without a database.</summary>
    public sealed class MemoryReceipts : IReceiptRepository
    {
        public List<EleFi.Domain.Receipts.Receipt> Rows { get; } = [];

        public List<EleFi.Domain.Receipts.ReceiptImage> Images { get; } = [];

        public Task AddAsync(EleFi.Domain.Receipts.Receipt receipt, EleFi.Domain.Receipts.ReceiptImage? image, CancellationToken cancellationToken = default)
        {
            Rows.Add(receipt);
            if (image is not null)
            {
                Images.Add(image);
            }

            return Task.CompletedTask;
        }

        public Task<EleFi.Domain.Receipts.Receipt?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.Reference == reference && r.DeletedAt is null));

        public Task<EleFi.Domain.Receipts.Receipt?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.Id == id && r.DeletedAt is null));

        public Task<IReadOnlyList<EleFi.Domain.Receipts.Receipt>> ForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EleFi.Domain.Receipts.Receipt>>([.. Rows.Where(r => r.TransactionId == transactionId && r.DeletedAt is null)]);

        public Task<IReadOnlyDictionary<Guid, byte[]>> ThumbnailsAsync(IReadOnlyCollection<Guid> receiptIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, byte[]>>(Images.Where(i => receiptIds.Contains(i.ReceiptId)).ToDictionary(i => i.ReceiptId, i => i.Thumbnail));

        public Task<byte[]?> PhotoAsync(Guid receiptId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Images.FirstOrDefault(i => i.ReceiptId == receiptId)?.Photo);

        public Task UpdateAsync(EleFi.Domain.Receipts.Receipt receipt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Rows.Where(r => r.Id == id).ToList().ForEach(r => r.DeletedAt = DateTimeOffset.UnixEpoch);
            return Task.CompletedTask;
        }

        public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Rows.Where(r => r.Id == id).ToList().ForEach(r => r.DeletedAt = null);
            return Task.CompletedTask;
        }
    }

    /// <summary>A share sheet that shares nothing, for pages that only offer to.</summary>
    public sealed class NoShare : IFileShare
    {
        public Task<string> ShareAsync(string fileName, ReadOnlyMemory<byte> contents, string title, CancellationToken cancellationToken = default) =>
            Task.FromResult(fileName);
    }

    /// <summary>A scanner that hands back a prepared receipt, as if it had been shared or photographed.</summary>
    public sealed class FixedScanner(ScannedReceipt? scan) : IReceiptScanner
    {
        public bool? AskedForCamera { get; private set; }

        public Task<ScannedReceipt?> TakeSharedAsync(CancellationToken cancellationToken = default) => Task.FromResult(scan);

        public Task<ScannedReceipt?> PickAsync(bool camera, CancellationToken cancellationToken = default)
        {
            AskedForCamera = camera;
            return Task.FromResult(scan);
        }
    }
}
