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
    private WriterFlowMark? _mark;

    private UnitOfWork(DbConnection connection, ProviderTransaction started, ISqlProvider provider, TransactionTrace?
            trace)
    {
        _trace = trace;
        _connection = connection;
        _transaction = started.Transaction;
        _release = started.Release;
        _provider = provider;
    }

    // The writer-flow mark is installed here, synchronously, so it flows to the caller; one installed inside the async
    // core would not. Maintenance reads it to refuse taking a writer gate this flow already holds.
    public static Task<UnitOfWork> BeginAsync(OrmContext context, CancellationToken ct = default) =>
        BeginMarkedAsync(context, ct, WriterFlow.Enter());

    internal static Task<UnitOfWork> BeginGraphSaveAsync(OrmContext context, CancellationToken ct) =>
        BeginCoreAsync(context, ct, true);

    // The writer key is read before the transaction begins, so a provider that cannot name it leaves nothing open; the
    // mark carries it only once the transaction has begun. Any failure closes the mark.
    private static async Task<UnitOfWork> BeginMarkedAsync(OrmContext context, CancellationToken ct, WriterFlowMark mark)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(context);
            var key = (context.Provider as IMaintenanceProvider)?.WriterKeyOf(context.DataSource);
            var unit = await BeginCoreAsync(context, ct, false);
            unit._mark = mark;
            mark.Key = key;
            return unit;
        }
        catch
        {
            mark.Close();
            throw;
        }
    }

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

    internal static UnitOfWork AttachOrphan(OrphanUnitAcquire acquired)
    {
        var connection = acquired.Connection!;
        var started = acquired.Started!.Value;
        acquired.Trace = TransactionTrace.Begin(connection);
        var unit = new UnitOfWork(connection, started, acquired.Operation.Provider, acquired.Trace);
        acquired.Unit = unit;
        s_open.Add(connection, started);
        PolicyOrigins.Record(connection, acquired.Operation.Context, unit);
        return unit;
    }

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
        _mark?.Close();
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
        cleanup.Run(() => _mark?.Close());
        cleanup.ThrowIfFailed();
    }

}
