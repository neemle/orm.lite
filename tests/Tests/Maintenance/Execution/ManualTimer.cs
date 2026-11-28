namespace Neemle.OrmLite.Tests.Maintenance.Execution;

internal sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
{
    private readonly List<TimeSpan> _arms = [];

    // When the timer fires, on the clock; null when it is not armed.
    internal TimeSpan? DueAt { get; private set; }

    // Every due time it was armed with, relative to when it was armed.
    internal IReadOnlyList<TimeSpan> Arms => _arms;

    internal bool Disposed { get; private set; }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        if (Disposed) return false;
        _arms.Add(dueTime);
        DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.Now + dueTime;
        return true;
    }

    // Runs the callback now, armed or not: the test decides when a timer is early, late or already dequeued.
    internal void Fire()
    {
        DueAt = null;
        callback(state);
    }

    public void Dispose()
    {
        Disposed = true;
        DueAt = null;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
