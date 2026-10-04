using EleFi.Application.Abstractions;
using EleFi.Domain.Alerts;

namespace EleFi.Application.Tests.Support;

/// <summary>
/// A suggestion store in a list, with the shipped rules already in it.
/// </summary>
/// <remarks>
/// The query semantics that matter (fingerprint uniqueness, hard delete) are tested against a
/// real database in EleFi.Infrastructure.Tests. This only has to behave the same way for the
/// service's orchestration to be tested here.
/// </remarks>
internal sealed class InMemorySuggestions : ISuggestionRepository
{
    public List<CaptureSuggestion> Rows { get; } = [];

    public List<ParseRule> Rules { get; } = [.. BuiltInParseRules.Create(DateTimeOffset.UnixEpoch)];

    public Task<IReadOnlyList<ParseRule>> ListRulesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ParseRule>>(Rules);

    public Task<IReadOnlyList<CaptureSuggestion>> ListPendingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CaptureSuggestion>>(Rows.Where(r => r.IsPending).ToList());

    public Task<IReadOnlyList<CaptureSuggestion>> ListSinceAsync(
        DateTimeOffset since, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CaptureSuggestion>>(Rows.Where(r => r.CreatedAt >= since).ToList());

    public Task<int> CountPendingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.Count(r => r.IsPending));

    public Task<bool> ExistsAsync(string fingerprint, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.Exists(r => r.Fingerprint == fingerprint || r.CorroboratingFingerprint == fingerprint));

    public Task AddAsync(CaptureSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        Rows.Add(suggestion);
        return Task.CompletedTask;
    }

    public int Updates { get; private set; }

    public Task UpdateAsync(CaptureSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        Updates++;
        return Task.CompletedTask;
    }

    public Task<CaptureSuggestion?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.Find(r => r.Id == id));

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Rows.RemoveAll(r => r.Id == id);
        return Task.CompletedTask;
    }

    public Task MarkConfirmedAsync(Guid id, Guid transactionId, CancellationToken cancellationToken = default)
    {
        var row = Rows.Find(r => r.Id == id);
        if (row is not null)
        {
            row.State = SuggestionState.Confirmed;
            row.TransactionId = transactionId;
        }

        return Task.CompletedTask;
    }

    public Task<int> PurgeExpiredAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.RemoveAll(r => r.IsPending && r.ExpiresAt <= asOf));
}
