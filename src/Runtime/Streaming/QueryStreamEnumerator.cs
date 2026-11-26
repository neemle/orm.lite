using System.Runtime.ExceptionServices;

namespace Neemle.OrmLite;

public sealed class QueryStreamEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly object _sync = new();
    private readonly StreamReadSnapshot<T> _snapshot;
    private readonly StreamCursor<T> _cursor;
    private readonly StreamTokens _tokens;
    private readonly StreamCleanup _cleanup = new();
    private readonly TaskCompletionSource<QueryStreamCompletion> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task<bool>? _advance;
    private Task? _runner;
    private QueryStreamError _category = QueryStreamError.ProviderFailure;
    private Task? _dispose;
    private Task? _disposeRunner;
    private QueryStreamStopReason? _stop;
    private ExceptionDispatchInfo? _fault;
    private bool _advancing;
    private bool _opened;
    private bool _disposeRequested;
    private bool _currentValid;
    private T _current = default!;
    private long _rows;

    internal QueryStreamEnumerator(StreamReadSnapshot<T> snapshot, CancellationToken terminal,
        CancellationToken enumeration)
    {
        _snapshot = snapshot;
        _cursor = new StreamCursor<T>(snapshot);
        _tokens = new StreamTokens(terminal, enumeration);
    }

    public T Current
    {
        get
        {
            lock (_sync)
            {
                if (!_currentValid) throw new QueryStreamException(QueryStreamError.InvalidState, _rows);
                return _current;
            }
        }
    }

    public Task<QueryStreamCompletion> Completion => _completion.Task;

    public ValueTask<bool> MoveNextAsync()
    {
        lock (_sync)
        {
            if (_advancing) return ValueTask.FromException<bool>(
                new QueryStreamException(QueryStreamError.ConcurrentAdvance, _rows));
            if (_fault is { } fault) return ValueTask.FromException<bool>(fault.SourceException);
            if (_stop is not null || _disposeRequested) return ValueTask.FromResult(false);
            _advancing = true;
            ClearCurrent();
            var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _advance = result.Task;
            _runner = AdvanceAsync(result);
            return new ValueTask<bool>(_advance);
        }
    }

    private async Task AdvanceAsync(TaskCompletionSource<bool> result)
    {
        try
        {
            bool next;
            try { next = await PullAsync(); }
            catch (Exception error) { next = await FailedAsync(error); }
            lock (_sync) _advancing = false;
            result.TrySetResult(next);
        }
        catch (Exception error)
        {
            lock (_sync) _advancing = false;
            result.TrySetException(error);
        }
    }

    private async Task<bool> PullAsync()
    {
        var ct = _tokens.Effective();
        ct.ThrowIfCancellationRequested();
        if (!_opened)
        {
            await _cursor.OpenAsync(ct);
            _opened = true;
        }
        _cursor.Validate();
        ct.ThrowIfCancellationRequested();
        var available = await _cursor.Reader.ReadAsync(ct);
        ct.ThrowIfCancellationRequested();
        _cursor.Validate();
        if (!available)
        {
            await FinishAsync(QueryStreamStopReason.Completed, null);
            ThrowFault();
            return false;
        }
        return MapAndPublish(ct);
    }

    private bool MapAndPublish(CancellationToken ct)
    {
        var row = Map();
        ct.ThrowIfCancellationRequested();
        _cursor.Validate();
        return Publish(row, ct);
    }

    private T Map()
    {
        try { return _snapshot.Map(_cursor.Reader); }
        catch (Exception error) { throw new StreamMappingFailure(error); }
    }

    private bool Publish(T row, CancellationToken ct)
    {
        lock (_sync)
        {
            if (_disposeRequested) throw new OperationCanceledException(ct);
            _current = row;
            _currentValid = true;
            _rows++;
            return true;
        }
    }

    private async Task<bool> FailedAsync(Exception error)
    {
        if (_stop is not null) { ThrowFault(); return false; }
        var mapping = error is StreamMappingFailure;
        _category = mapping ? QueryStreamError.MappingFailure : QueryStreamError.ProviderFailure;
        var primary = mapping ? error.InnerException! : error;
        if (primary is StreamOpenFailure open)
        {
            primary = open.Primary;
            _cleanup.Add(open.Cleanup);
        }
        var reason = FailureReason(primary, mapping);
        await FinishAsync(reason, primary);
        if (reason != QueryStreamStopReason.Disposed) ThrowFault();
        return false;
    }

    private QueryStreamStopReason FailureReason(Exception primary, bool mapping)
    {
        if (primary is not OperationCanceledException) return QueryStreamStopReason.Faulted;
        if (_tokens.UserCanceled) return QueryStreamStopReason.Canceled;
        if (_disposeRequested && !mapping) return QueryStreamStopReason.Disposed;
        return QueryStreamStopReason.Faulted;
    }

    private async Task FinishAsync(QueryStreamStopReason reason, Exception? primary)
    {
        await _tokens.BeginCleanupAsync();
        await _cursor.CleanupAsync(_cleanup);
        _tokens.Cleanup(_cleanup);
        var errors = _cleanup.Errors;
        if (reason == QueryStreamStopReason.Completed && errors.Count > 0)
            reason = QueryStreamStopReason.Faulted;
        lock (_sync)
        {
            ClearCurrent();
            _stop = reason;
            _fault = Fault(reason, primary, errors);
            _completion.TrySetResult(new QueryStreamCompletion(reason, _rows, primary, errors));
        }
    }

    private ExceptionDispatchInfo? Fault(QueryStreamStopReason reason, Exception? primary,
        IReadOnlyList<Exception> cleanup)
    {
        if (reason == QueryStreamStopReason.Canceled)
            return ExceptionDispatchInfo.Capture(new QueryStreamCanceledException(_rows, cleanup,
                primary ?? new OperationCanceledException(_tokens.UserToken), _tokens.UserToken));
        if (reason != QueryStreamStopReason.Faulted) return null;
        var error = primary switch
        {
            null => QueryStreamError.CleanupFailure,
            _ when _category == QueryStreamError.MappingFailure => QueryStreamError.MappingFailure,
            QueryStreamException stream => stream.Error,
            _ when primary is OperationCanceledException => QueryStreamError.MappingFailure,
            _ => _category,
        };
        return ExceptionDispatchInfo.Capture(new QueryStreamException(error, _rows, cleanup, primary));
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource result;
        Task<bool>? advance;
        lock (_sync)
        {
            if (_dispose is not null) return new ValueTask(_dispose);
            _disposeRequested = true;
            ClearCurrent();
            result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _dispose = result.Task;
            advance = _advance;
        }
        _tokens.RequestDispose(_cleanup);
        _disposeRunner = CompleteDisposeAsync(advance, result);
        return new ValueTask(result.Task);
    }

    private async Task CompleteDisposeAsync(Task<bool>? advance, TaskCompletionSource result)
    {
        try { await DisposeCoreAsync(advance); }
        finally { result.TrySetResult(); }
    }

    private async Task DisposeCoreAsync(Task<bool>? advance)
    {
        if (advance is not null)
        {
            try { await advance; }
            catch (Exception) { }
        }
        if (_runner is { } runner) await runner;
        if (_completion.Task.IsCompleted) return;
        var reason = _tokens.UserCanceled ? QueryStreamStopReason.Canceled : QueryStreamStopReason.Disposed;
        var primary = reason == QueryStreamStopReason.Canceled
            ? new OperationCanceledException(_tokens.UserToken) : null;
        await FinishAsync(reason, primary);
    }

    private void ClearCurrent() { _current = default!; _currentValid = false; }

    private void ThrowFault() => _fault?.Throw();
}
