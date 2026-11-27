namespace Neemle.OrmLite;

public sealed class OrphanReplacement<TChild, TChildKey> : IAsyncDisposable
    where TChild : class where TChildKey : notnull
{
    private readonly object _sync = new();
    private readonly OrphanOperation _operation;
    private readonly OrphanUnitAcquire? _owner;
    private readonly IReadOnlyList<TChildKey> _original;
    private bool _consumed;
    private bool _ended;
    private Task<OrphanResult>? _apply;
    private Task? _dispose;

    internal OrphanReplacement(OrphanOperation operation, OrphanUnitAcquire? owner, IReadOnlyList<TChildKey> original)
    {
        _operation = operation;
        _owner = owner;
        _original = Array.AsReadOnly(original.ToArray());
        Observation = new(_original, owner is not null);
    }

    public OrphanObservation<TChildKey> Observation { get; }

    public Task<OrphanResult> ApplyAsync(OrphanReplacementRequest<TChild, TChildKey> request,
        CancellationToken ct = default)
    {
        TaskCompletionSource<Task<OrphanResult>> start;
        Task<OrphanResult> apply;
        lock (_sync)
        {
            if (_ended) throw OrphanRefusal.At(OrphanError.ScopeEnded);
            if (_consumed) throw OrphanRefusal.At(OrphanError.SnapshotConsumed);
            _consumed = true;
            start = new(TaskCreationOptions.RunContinuationsAsynchronously);
            apply = _apply = start.Task.Unwrap();
        }
        start.SetResult(ApplyCoreAsync(request, ct));
        return apply;
    }

    private async Task<OrphanResult> ApplyCoreAsync(OrphanReplacementRequest<TChild, TChildKey> request,
        CancellationToken ct)
    {
        if (_operation.Options.Mode != OrphanMode.Disabled)
            return await OrphanApply<TChild, TChildKey>.RunAsync(_owner!, _original, request, ct);
        if (request?.PresentWrites is null || request.PresentWrites.Items.Count != 0)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        if (ct.IsCancellationRequested)
            throw new OrphanCanceledException(OrphanOutcome.NotStarted, ct, new OperationCanceledException(ct));
        return new OrphanResult(OrphanOutcome.NotStarted, true, new(0, 0, 0, 0, 0));
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource<Task> start;
        Task dispose;
        Task<OrphanResult>? apply;
        lock (_sync)
        {
            if (_dispose is not null) return new(_dispose);
            _ended = true;
            start = new(TaskCreationOptions.RunContinuationsAsynchronously);
            dispose = _dispose = start.Task.Unwrap();
            apply = _apply;
        }
        start.SetResult(DisposeCoreAsync(apply));
        return new(dispose);
    }

    private async Task DisposeCoreAsync(Task<OrphanResult>? apply)
    {
        if (apply is not null)
        {
            try { await apply; }
            catch (Exception) { }
        }
        else if (_owner is not null) await OrphanFinish.AbandonAsync(_owner);
    }
}
