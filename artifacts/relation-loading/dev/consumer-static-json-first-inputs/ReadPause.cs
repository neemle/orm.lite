namespace PolicySmoke;

internal sealed class ReadPause(string contains) : IDisposable
{
    private readonly ManualResetEventSlim _release = new();
    internal string Contains { get; } = contains;
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void Wait()
    {
        Entered.TrySetResult();
        if (!_release.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("fixture pause timed out");
    }

    internal void Release() => _release.Set();
    public void Dispose() { Release(); _release.Dispose(); }
}
