namespace Neemle.OrmLite;

internal sealed partial class MaintenanceExecution
{
    /// <summary>
    /// C8: the call's one budget on one injected clock, with one latched expiry. Admission reads the clock; the timer
    /// only cancels the open's token, and only once the clock says the budget is spent, so the two can never disagree.
    /// </summary>
    private sealed class Deadline(TimeSpan total, TimeProvider clock, CancellationToken caller) : IDisposable
    {
        // Timers and command timeouts take at most int.MaxValue milliseconds; a longer budget is held to that and
        // re-armed.
        private static readonly TimeSpan Longest = TimeSpan.FromMilliseconds(int.MaxValue);

        private readonly long _start = clock.GetTimestamp();

        private readonly Lock _gate = new();

        private CancellationTokenSource? _source;

        private ITimer? _timer;

        private int _expired;

        private bool _disposed;

        internal bool IsExpired => Volatile.Read(ref _expired) != 0;

        // What is left, held to int.MaxValue milliseconds, zero once spent. Reading it decides nothing.
        internal TimeSpan Remaining
        {
            get
            {
                var left = total - clock.GetElapsedTime(_start);
                if (left <= TimeSpan.Zero) return TimeSpan.Zero;
                return left > Longest ? Longest : left;
            }
        }

        // The token of the open: the caller's, linked to the deadline. The timer is armed the first time it is asked
        // for.
        internal CancellationToken Token
        {
            get
            {
                lock (_gate)
                {
                    if (_source is null)
                    {
                        _source = CancellationTokenSource.CreateLinkedTokenSource(caller);
                        _timer = clock.CreateTimer(Fire, null, Due(), Timeout.InfiniteTimeSpan);
                    }
                    return _source.Token;
                }
            }
        }

        // Admission's clock read: what an admitted effect may spend, or null once the budget is spent, which latches.
        internal TimeSpan? Admit()
        {
            if (IsExpired) return null;
            var left = Remaining;
            if (left > TimeSpan.Zero) return left;
            Volatile.Write(ref _expired, 1);
            return null;
        }

        // The remaining time rounded UP to whole milliseconds: a timer due any earlier could fire while time is left.
        private TimeSpan Due() => TimeSpan.FromMilliseconds(Math.Ceiling(Remaining.TotalMilliseconds));

        // The timer re-reads the clock: it latches and cancels only when the budget is spent, and otherwise re-arms (a
        // clamped or early timer).
        private void Fire(object? state)
        {
            lock (_gate)
            {
                if (_disposed) return;
                if (clock.GetElapsedTime(_start) < total)
                {
                    _timer!.Change(Due(), Timeout.InfiniteTimeSpan);
                    return;
                }
                Volatile.Write(ref _expired, 1);
                _source!.Cancel();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _timer?.Dispose();
                _source?.Dispose();
            }
        }
    }
}
