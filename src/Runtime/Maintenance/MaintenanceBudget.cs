using System.Diagnostics;

namespace Neemle.OrmLite;

/// <summary>The one timeout budget of a maintenance call: every gate wait, lock wait and statement spends from it.</summary>
internal sealed class MaintenanceBudget(TimeSpan total)
{
    // SemaphoreSlim and CommandTimeout take at most int.MaxValue milliseconds and seconds; a longer budget is held to that.
    private static readonly TimeSpan Longest = TimeSpan.FromMilliseconds(int.MaxValue);

    private readonly long _start = Stopwatch.GetTimestamp();

    internal TimeSpan Remaining
    {
        get
        {
            var left = total - Stopwatch.GetElapsedTime(_start);
            if (left <= TimeSpan.Zero) return TimeSpan.Zero;
            return left > Longest ? Longest : left;
        }
    }

    // ADR D12.3: a spent budget starts nothing more; the caller reports Busy.
    internal bool IsSpent => Remaining == TimeSpan.Zero;

    // A whole number of seconds, at least one, for a budget with time left: callers check IsSpent first and start
    // nothing on a spent budget (D12.3), so the floor only keeps a nearly spent budget from rounding down to zero, which
    // a driver reads as no timeout. With a retry slice, no more than the slice.
    internal int CommandSeconds(int grace, TimeSpan? slice = null)
    {
        var remaining = Remaining;
        var wait = slice is { } most && most < remaining ? most : remaining;
        var seconds = Math.Ceiling(wait.TotalSeconds);
        return (int)Math.Min(int.MaxValue - grace, Math.Max(1, seconds)) + grace;
    }
}
