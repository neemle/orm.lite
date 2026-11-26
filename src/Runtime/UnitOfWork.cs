#nullable enable
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public sealed class UnitOfWork : IAsyncDisposable
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbConnection, object> s_open = new();

    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private readonly ISqlProvider _provider;
    private IAsyncDisposable? _release;
    private readonly TransactionTrace? _trace;
    internal PolicyLifetime PolicyLifetime { get; } = new();

    private bool _committed;
    private bool _disposed;

    private UnitOfWork(DbConnection connection, ProviderTransaction started, ISqlProvider provider, TransactionTrace?
            trace)
    {
        _trace = trace;
        _connection = connection;
        _transaction = started.Transaction;
        _release = started.Release;
        _provider = provider;
    }

    public static Task<UnitOfWork> BeginAsync(OrmContext context, CancellationToken ct = default) =>
        BeginCoreAsync(context, ct, false);

    internal static Task<UnitOfWork> BeginGraphSaveAsync(OrmContext context, CancellationToken ct) =>
        BeginCoreAsync(context, ct, true);

    private static async Task<UnitOfWork> BeginCoreAsync(OrmContext context, CancellationToken ct, bool graphSave)
    {
        ArgumentNullException.ThrowIfNull(context);
        var connection = graphSave ? await GraphSaveOpen.ConnectionAsync(context, ct)
            : await context.DataSource.OpenConnectionAsync(ct);
        var acquired = new GraphSaveAcquire(connection, () => s_open.Remove(connection));
        try
        {
            var started = await context.Provider.BeginTransactionAsync(connection, context.DataSource, ct);
            acquired.Started = started;
            s_open.AddOrUpdate(connection, started);
            acquired.Trace = TransactionTrace.Begin(connection);
            var unit = new UnitOfWork(connection, started, context.Provider, acquired.Trace);
            acquired.Unit = unit;
            PolicyOrigins.Record(connection, context, unit);
            return unit;
        }
        catch (Exception primary) { await acquired.FailedAsync(primary); throw; }
    }

    public DbConnection Connection
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _connection;
        }
    }

    public DbTransaction Transaction
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _transaction;
        }
    }

    public bool Committed => _committed;

    internal ISqlProvider Provider => _provider;

    internal static bool IsOpenOn(DbConnection connection) => s_open.TryGetValue(connection, out _);

    [RequiresCapability(Capability.NamedLock)]
    public Task LockAsync(string key) => LockCoreAsync(NamedLockKind.Exclusive, key);

    [RequiresCapability(Capability.NamedLock)]
    public Task LockSharedAsync(string key) => LockCoreAsync(NamedLockKind.Shared, key);

    private async Task LockCoreAsync(NamedLockKind kind, string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var statement = StatementCompiler.Compile(
            new Select([new NamedLock(kind, new Param(0, key, "k"))]), _provider, StatementKind.Write);
        await using var cmd = _connection.CreateCommand();
        cmd.Transaction = _transaction;
        cmd.CommandText = statement.Text;
        foreach (var parameter in statement.Parameters)
            cmd.AddParam(parameter.Name, parameter.Value);
        await cmd.RunNonQueryAsync(statement.Kind);
    }

    public async Task CommitAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_committed)
            throw new InvalidOperationException(
                "this unit of work has already been committed; a second commit would silently succeed "
                + "against a transaction that no longer exists and hide a double-run of the composition.");
        await PolicyLifetime.CloseAsync();
        await _transaction.CommitAsync();
        _committed = true;
        _trace?.Commit();
        await ReleaseAsync();
    }

    private async ValueTask ReleaseAsync()
    {
        var release = Interlocked.Exchange(ref _release, null);
        if (release is not null) await release.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        var cleanup = new PolicyCleanupSteps();
        await cleanup.RunAsync(PolicyLifetime.CloseAsync);
        cleanup.Run(() => _trace?.Dispose());
        cleanup.Run(() => s_open.Remove(_connection));
        await cleanup.RunAsync(() => _transaction.DisposeAsync().AsTask());
        await cleanup.RunAsync(() => _connection.DisposeAsync().AsTask());
        await cleanup.RunAsync(() => ReleaseAsync().AsTask());
        cleanup.ThrowIfFailed();
    }

}
