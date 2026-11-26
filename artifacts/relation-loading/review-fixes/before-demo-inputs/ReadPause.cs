namespace PolicySmoke;

internal sealed class ReadPause(string contains, int skip = 0) : IDisposable
{
    private readonly ManualResetEventSlim _release = new();
    private int _matches;
    internal string Contains { get; } = contains;
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void Wait()
    {
        if (Interlocked.Increment(ref _matches) <= skip) return;
        Entered.TrySetResult();
        if (!_release.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("fixture pause timed out");
    }

    internal void Release() => _release.Set();
    public void Dispose() { Release(); _release.Dispose(); }
}
