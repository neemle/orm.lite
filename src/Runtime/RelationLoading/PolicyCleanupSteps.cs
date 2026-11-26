using System.Runtime.ExceptionServices;

namespace Neemle.OrmLite;

internal sealed class PolicyCleanupSteps(Exception? primary = null)
{
    private ExceptionDispatchInfo? _failure = primary is null ? null : ExceptionDispatchInfo.Capture(primary);
    internal void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { Record(error); }
    }
    internal async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception error) { Record(error); }
    }
    private void Record(Exception error)
    {
        if (_failure is null) _failure = ExceptionDispatchInfo.Capture(error);
        else PolicyCleanup.Retain(_failure.SourceException, error);
    }
    internal void ThrowIfFailed() => _failure?.Throw();
}
