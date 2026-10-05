using EleFi.Application.Abstractions;
using EleFi.Domain.Notices;
using EleFi.Domain.Planning;

namespace EleFi.Application.Tests.Support;

/// <summary>Plans in a list.</summary>
internal sealed class InMemoryPlans : IPlanRepository
{
    public List<Plan> Rows { get; } = [];

    private IEnumerable<Plan> Live => Rows.Where(p => p.DeletedAt is null);

    public Task<IReadOnlyList<Plan>> ListOpenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Plan>>(Live.Where(p => !p.IsDone).OrderBy(p => p.DueOn).ToList());

    public Task<IReadOnlyList<Plan>> ListCompletedAsync(int take = 100, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Plan>>(Live.Where(p => p.IsDone).OrderByDescending(p => p.CompletedAt).Take(take).ToList());

    public Task<Plan?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Live.FirstOrDefault(p => p.Id == id));

    public Task AddAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        Rows.Add(plan);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Plan plan, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Rows.Find(p => p.Id == id)!.DeletedAt = DateTimeOffset.UnixEpoch;
        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<Guid>> ClaimedTransactionIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(Live.Where(p => p.TransactionId is not null).Select(p => p.TransactionId!.Value).ToHashSet());
}

/// <summary>Notifications in a list.</summary>
internal sealed class InMemoryNotices : INoticeRepository
{
    public List<Notice> Rows { get; } = [];

    public Task<IReadOnlyList<Notice>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Notice>>(Rows.OrderByDescending(n => n.CreatedAt).ToList());

    public Task<int> UnreadCountAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.Count(n => !n.IsRead));

    public Task UpsertAsync(Notice notice, CancellationToken cancellationToken = default)
    {
        var existing = Rows.Find(n => n.Route is not null && n.Route == notice.Route);
        if (existing is null)
        {
            Rows.Add(notice);
        }
        else
        {
            existing.Title = notice.Title;
            existing.Body = notice.Body;
            existing.ReadAt = null;
        }

        return Task.CompletedTask;
    }

    public Task SetReadAsync(Guid id, bool read, CancellationToken cancellationToken = default)
    {
        Rows.Find(n => n.Id == id)!.ReadAt = read ? DateTimeOffset.UnixEpoch : null;
        return Task.CompletedTask;
    }

    public Task SetAllReadAsync(bool read, CancellationToken cancellationToken = default)
    {
        Rows.ForEach(n => n.ReadAt = read ? DateTimeOffset.UnixEpoch : null);
        return Task.CompletedTask;
    }
}
