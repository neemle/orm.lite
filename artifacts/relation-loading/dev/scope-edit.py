from pathlib import Path
p=Path('src/Runtime/RelationLoading')
f={
'ScopeBinding.cs':'''using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class ScopeBinding(OrmContext context, DbConnection connection, DbTransaction? transaction,
    LoadingScopeOptions options)
{
    internal OrmContext Context { get; } = context;
    internal DbConnection Connection { get; } = connection;
    internal DbTransaction? Transaction { get; } = transaction;
    internal LoadingScopeOptions Options { get; } = options;
    internal UnitOfWork? Unit { get; init; }
    internal bool OwnsConnection { get; init; }

    internal void Validate()
    {
        try
        {
            if (Connection.State != ConnectionState.Open || Transaction is { } tx && tx.Connection != Connection)
                throw new RelationScopeException(RelationScopeError.InvalidResource);
            if (Unit is { } unit && (unit.Committed || unit.Connection != Connection || unit.Transaction != Transaction))
                throw new RelationScopeException(RelationScopeError.InvalidResource);
        }
        catch (ObjectDisposedException) { throw new RelationScopeException(RelationScopeError.InvalidResource); }
    }

    internal void Match<T>(GraphQueryState<T> state)
    {
        if (!ReferenceEquals(state.Database, Context)
            || state.Connection is { } conn && (!ReferenceEquals(conn, Connection)
                || !ReferenceEquals(state.Transaction, Transaction)))
            throw new RelationScopeException(RelationScopeError.DifferentScope);
        Validate();
    }
}
''',
'ScopeClock.cs':'''using System.Diagnostics;

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
        Tick(null);
    }

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
''',
'ScopeScheduler.cs':'''namespace Neemle.OrmLite;

internal sealed class ScopeScheduler(LoadingScope owner)
{
    private readonly SemaphoreSlim _serial = new(1, 1);
    private readonly HashSet<Task> _operations = [];
    private readonly CancellationTokenSource _stop = new();
    private RelationScopeError? _closed;

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
''',
'LoadingScope.cs':'''using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite;

public sealed class LoadingScope : IAsyncDisposable
{
    private Task? _closing;
    internal object Gate { get; } = new();
    internal ScopeBinding Binding { get; }
    internal ScopeScheduler Scheduler { get; }
    internal ScopeAttachments Attachments { get; }
    internal ScopeClock Clock { get; }

    private LoadingScope(ScopeBinding binding)
    {
        Binding = binding;
        Scheduler = new ScopeScheduler(this);
        Attachments = new ScopeAttachments(this);
        Clock = new ScopeClock(binding.Options.Lifetime, () => _ = CloseAsync(RelationScopeError.Expired));
        binding.Context.PolicyLifetime.Add(this);
        try { binding.Unit?.PolicyLifetime.Add(this); }
        catch { binding.Context.PolicyLifetime.Remove(this); Clock.Dispose(); throw; }
        binding.Connection.StateChange += OnStateChange;
    }

    public static async Task<LoadingScope> OpenAsync(OrmContext context, LoadingScopeOptions options,
        CancellationToken ct = default)
    {
        ValidateOptions(context, options);
        var connection = await context.DataSource.OpenConnectionAsync(ct);
        try { return new LoadingScope(new ScopeBinding(context, connection, null, options) { OwnsConnection = true }); }
        catch { await connection.DisposeAsync(); throw; }
    }

    public static LoadingScope Bind(OrmContext context, UnitOfWork unitOfWork, LoadingScopeOptions options)
    {
        ValidateOptions(context, options);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        if (!ReferenceEquals(unitOfWork.Provider, context.Provider))
            throw new RelationScopeException(RelationScopeError.DifferentScope);
        var binding = new ScopeBinding(context, unitOfWork.Connection, unitOfWork.Transaction, options)
            { Unit = unitOfWork };
        binding.Validate();
        return new LoadingScope(binding);
    }

    public static LoadingScope Bind(OrmContext context, DbConnection connection, DbTransaction? transaction,
        LoadingScopeOptions options)
    {
        ValidateOptions(context, options);
        ArgumentNullException.ThrowIfNull(connection);
        var binding = new ScopeBinding(context, connection, transaction, options);
        binding.Validate();
        return new LoadingScope(binding);
    }

    public Task LoadAsync<T, TTarget>(T entity, GraphPath<T, TTarget> navigation, CancellationToken ct = default)
        where T : class where TTarget : class => Attachments.Demand(entity, navigation, false, ct);

    public Task RetryAsync<T, TTarget>(T entity, GraphPath<T, TTarget> navigation, CancellationToken ct = default)
        where T : class where TTarget : class => Attachments.Demand(entity, navigation, true, ct);

    public RelationLoadSnapshot State<T, TTarget>(T entity, GraphPath<T, TTarget> navigation)
        where T : class where TTarget : class => Attachments.State(entity, navigation);

    public void Detach<T>(T entity) where T : class => Attachments.Detach(entity);

    private static void ValidateOptions(OrmContext context, LoadingScopeOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (options?.Limits is null || options.MaxPendingDemands <= 0 || options.Lifetime <= TimeSpan.Zero)
            throw new RelationScopeException(RelationScopeError.InvalidOptions);
    }

    private void OnStateChange(object? sender, StateChangeEventArgs e)
    {
        if (e.CurrentState is ConnectionState.Closed or ConnectionState.Broken)
            _ = CloseAsync(RelationScopeError.InvalidResource);
    }

    internal Task CloseFromOwnerAsync() => CloseAsync(RelationScopeError.Closed);

    private Task CloseAsync(RelationScopeError error)
    {
        lock (Gate) return _closing ??= CloseCoreAsync(error);
    }

    private async Task CloseCoreAsync(RelationScopeError error)
    {
        await Scheduler.CloseAsync(error);
        Attachments.Invalidate();
        Clock.Dispose();
        Binding.Connection.StateChange -= OnStateChange;
        Binding.Context.PolicyLifetime.Remove(this);
        Binding.Unit?.PolicyLifetime.Remove(this);
        if (Binding.OwnsConnection) await Binding.Connection.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(CloseAsync(RelationScopeError.Closed));
}
'''
}
for n,s in f.items():(p/n).write_text(s)
