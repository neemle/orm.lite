namespace Neemle.OrmLite;

internal sealed class ScopeScheduler(LoadingScope owner)
{
    private readonly SemaphoreSlim _serial = new(1, 1);
    private readonly HashSet<Task> _operations = [];
    private readonly CancellationTokenSource _stop = new();
    private RelationScopeError? _closed;

    internal bool IsAttached => _closed is null && !owner.Clock.Expired;

    internal CancellationToken Stopping => _stop.Token;

    internal void Validate()
    {
        if (_closed is { } error) throw new RelationScopeException(error);
        if (owner.Clock.Expired) throw new RelationScopeException(RelationScopeError.Expired);
        owner.Binding.Validate();
    }

    internal Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (owner.Gate)
        {
            Validate();
            if (_operations.Count > owner.Binding.Options.MaxPendingDemands)
                throw new RelationScopeException(RelationScopeError.QueueFull);
            _operations.Add(done.Task);
        }
        return RunCoreAsync(operation, ct, done);
    }

    private async Task<T> RunCoreAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct,
        TaskCompletionSource done)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
        var entered = false;
        try
        {
            await _serial.WaitAsync(linked.Token);
            entered = true;
            lock (owner.Gate) Validate();
            return await operation(linked.Token);
        }
        finally
        {
            if (entered) _serial.Release();
            lock (owner.Gate) _operations.Remove(done.Task);
            done.TrySetResult();
        }
    }

    internal Task CloseAsync(RelationScopeError error)
    {
        Task[] operations;
        lock (owner.Gate) { _closed ??= error; operations = _operations.ToArray(); }
        _stop.Cancel();
        return Task.WhenAll(operations);
    }
}
