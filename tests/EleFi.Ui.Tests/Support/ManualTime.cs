namespace EleFi.Ui.Tests.Support;

/// <summary>
/// A clock for timers that only moves when a test moves it, and fires due timers on the
/// test's own thread as it does.
/// </summary>
/// <remarks>
/// So a test of "Ele leaves after four seconds" takes no time at all and never races a real
/// timer on the thread pool.
/// </remarks>
internal sealed class ManualTime : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    /// <summary>Moves time forward, firing every timer that falls due on the way.</summary>
    public void Advance(TimeSpan by)
    {
        _now += by;

        foreach (var timer in _timers.Where(t => t.DueAt is { } due && due <= _now).ToList())
        {
            timer.Fire();
        }
    }

    private sealed class ManualTimer(ManualTime time, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : time._now + dueTime;
            return true;
        }

        public void Fire()
        {
            DueAt = null;
            callback(state);
        }

        public void Dispose()
        {
            DueAt = null;
            time._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
