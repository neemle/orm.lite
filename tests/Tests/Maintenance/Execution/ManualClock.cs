using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// A TimeProvider whose time moves only when a test moves it. Every timer is queued with its due time and fires on the
// test's thread, in due order, when Advance reaches it; Skip moves time without firing (a late timer), and Fire runs a
// callback whenever the test says (an early timer, or one already dequeued when its owner disposed it).
internal sealed class ManualClock : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    private long _now;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _now);

    internal TimeSpan Now => TimeSpan.FromTicks(GetTimestamp());

    internal IReadOnlyList<ManualTimer> Timers
    {
        get
        {
            lock (_timers) return [.. _timers];
        }
    }

    internal ManualTimer Timer => Assert.Single(Timers);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_timers) _timers.Add(timer);
        return timer;
    }

    // Moves time and fires every armed timer that is due by then, a re-armed one included.
    internal void Advance(TimeSpan by)
    {
        Interlocked.Add(ref _now, by.Ticks);
        while (Timers.Where(t => t.DueAt is { } due && due <= Now).OrderBy(t => t.DueAt).FirstOrDefault() is { } next)
            next.Fire();
    }

    // Moves time without firing anything: every timer due by then is late.
    internal void Skip(TimeSpan by) => Interlocked.Add(ref _now, by.Ticks);
}
