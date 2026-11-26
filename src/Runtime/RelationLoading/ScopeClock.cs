using System.Diagnostics;

namespace Neemle.OrmLite;

internal sealed class ScopeClock : IDisposable
{
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly TimeSpan _lifetime;
    private readonly Action _expire;
    private readonly Timer _timer;

    internal ScopeClock(TimeSpan lifetime, Action expire)
    {
        _lifetime = lifetime;
        _expire = expire;
        _timer = new Timer(Tick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    internal void Start() => Tick(null);

    internal bool Expired => Stopwatch.GetElapsedTime(_started) >= _lifetime;

    private void Tick(object? _)
    {
        var remaining = _lifetime - Stopwatch.GetElapsedTime(_started);
        if (remaining <= TimeSpan.Zero) { _expire(); return; }
        var due = remaining < TimeSpan.FromDays(1) ? remaining : TimeSpan.FromDays(1);
        try { _timer.Change(due, Timeout.InfiniteTimeSpan); }
        catch (ObjectDisposedException) { }
    }

    public void Dispose() => _timer.Dispose();
}
