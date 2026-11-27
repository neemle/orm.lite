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

    // A whole number of seconds, at least one, so a budget that is nearly spent still lets the statement start.
    internal int CommandSeconds(int grace)
    {
        var seconds = Math.Ceiling(Remaining.TotalSeconds);
        return (int)Math.Min(int.MaxValue - grace, Math.Max(1, seconds)) + grace;
    }
}
