#nullable enable
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Neemle.OrmLite;

public sealed class GatedDataSource(ISqlProvider provider, DbDataSource request, DbDataSource background,
    DbDataSource ops) : DbDataSource
{
    private static readonly ConditionalWeakTable<DbConnection, Lease> s_leases = new();

    private readonly ISqlProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    private OrmDiagnostics _diagnostics = OrmDiagnostics.Disabled(provider);
    private readonly DbDataSource _request = Checked(provider, request, nameof(request));
    private readonly DbDataSource _background = Checked(provider, background, nameof(background));
    private readonly DbDataSource _ops = Checked(provider, ops, nameof(ops));

    internal GatedDataSource(ISqlProvider provider, DbDataSource request, DbDataSource background, DbDataSource ops,
            OrmDiagnostics diagnostics)
        : this(provider, request, background, ops) => _diagnostics = diagnostics;

    public ISqlProvider Provider => _provider;

    public override string ConnectionString => _background.ConnectionString;

    private static DbDataSource Checked(ISqlProvider? provider, DbDataSource? source, string name)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));
        if (source is null) throw new ArgumentNullException(name);
        provider.ValidateDataSource(name, source);
        return source;
    }

    private DbDataSource LaneSource(DbLaneKind lane) => lane switch
    {
        DbLaneKind.Request => _request,
        DbLaneKind.Background => _background,
        DbLaneKind.Ops => _ops,
        _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "GatedDataSource has no pool for this lane."),
    };

    private async ValueTask<DbConnection> LeaseAsync(DbConnection inner, CancellationToken ct)
    {
        if (s_leases.TryGetValue(inner, out var prior))
        {
            inner.StateChange -= prior.OnStateChange;
            s_leases.Remove(inner);
        }

        await inner.OpenAsync(ct);
        var lease = new Lease(DbLane.CurrentScope, _diagnostics, this);
        lease.Acquire();
        s_leases.Add(inner, lease);
        inner.StateChange += lease.OnStateChange;

        try
        {
            await (DbActor.Current is { } actor
                ? _provider.StampActorAsync(inner, actor, ct)
                : _provider.ClearActorAsync(inner, ct));
        }
        catch
        {
            await inner.CloseAsync();
            throw;
        }
        return inner;
    }

    protected override async ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken cancellationToken = default
            )
    {
        var connection = LaneSource(DbLane.Current).CreateConnection();
        try
        {
            return await LeaseAsync(connection, cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    internal async Task<DbConnection> OpenMetadataConnectionAsync(CancellationToken ct)
    {
        var connection = LaneSource(DbLane.Current).CreateConnection();
        try
        {
            DetachMetadataLease(connection);
            await connection.OpenAsync(ct);
            AttachMetadataLease(connection);
            return connection;
        }
        catch (Exception primary)
        {
            var errors = new List<Exception>();
            try { await connection.DisposeAsync(); }
            catch (Exception cleanup) { errors.Add(cleanup); }
            if (primary is OperationCanceledException && ct.IsCancellationRequested)
                throw new SchemaDiffCanceledException(primary, ct, errors);
            throw new SchemaDiffException(SchemaDiffError.ProviderFailure, primary, errors);
        }
    }

    private static void DetachMetadataLease(DbConnection connection)
    {
        if (s_leases.TryGetValue(connection, out var prior))
        {
            connection.StateChange -= prior.OnStateChange;
            s_leases.Remove(connection);
        }
    }

    private void AttachMetadataLease(DbConnection connection)
    {
        var lease = new Lease(DbLane.CurrentScope, _diagnostics, this);
        lease.Acquire();
        s_leases.Add(connection, lease);
        connection.StateChange += lease.OnStateChange;
    }

    internal async ValueTask<DbConnection> OpenStreamConnectionAsync(CancellationToken ct)
    {
        var connection = LaneSource(DbLane.Current).CreateConnection();
        try { return await StreamLeaseAsync(connection, ct); }
        catch (Exception primary)
        {
            var cleanup = new StreamCleanup();
            await cleanup.Attempt(() => new ValueTask(connection.CloseAsync()));
            var disposed = await cleanup.Attempt(connection.DisposeAsync);
            if (disposed) LeaseOf(connection)?.ReleaseStream();
            throw new StreamOpenFailure(primary, cleanup.Errors);
        }
    }

    internal ValueTask<DbConnection> OpenGraphSaveConnectionAsync(CancellationToken ct) =>
        GraphSaveOpen.LeaseAsync(LaneSource(DbLane.Current).CreateConnection(),
            connection => StreamLeaseAsync(connection, ct));

    private async ValueTask<DbConnection> StreamLeaseAsync(DbConnection inner, CancellationToken ct)
    {
        if (s_leases.TryGetValue(inner, out var prior))
        {
            inner.StateChange -= prior.OnStateChange;
            s_leases.Remove(inner);
        }
        await inner.OpenAsync(ct);
        var lease = new Lease(DbLane.CurrentScope, _diagnostics, this);
        lease.Acquire();
        s_leases.Add(inner, lease);
        inner.StateChange += lease.OnStateChange;
        await (DbActor.Current is { } actor
            ? _provider.StampActorAsync(inner, actor, ct) : _provider.ClearActorAsync(inner, ct));
        return inner;
    }

    protected override DbCommand CreateDbCommand(string? commandText = null) =>
        new GatedCommand(this, LaneSource(DbLane.Current).CreateConnection(), commandText);

    protected override DbConnection CreateDbConnection() =>
        throw Refused("CreateConnection", "OpenConnectionAsync");

    protected override DbConnection OpenDbConnection() =>
        throw Refused("OpenConnection", "OpenConnectionAsync");

    protected override DbBatch CreateDbBatch() =>
        throw Refused("CreateBatch",
            "CreateCommand and ExecuteNonQueryAsync, or a batch on a connection from OpenConnectionAsync");

    private static NotSupportedException Refused(string member, string instead) =>
        new($"GatedDataSource refuses {member}: every connection is leased through the gate, so use {instead} instead.")
            ;

    internal static Lease? LeaseOf(DbConnection? connection) =>
        connection is not null && s_leases.TryGetValue(connection, out var lease) ? lease : null;

    internal static bool OriginatesFrom(DbConnection connection, GatedDataSource source) =>
        LeaseOf(connection) is { IsCurrent: true } lease && lease.OriginatesFrom(source);

    internal sealed class Lease(DbLaneScope? scope, OrmDiagnostics diagnostics, GatedDataSource source)
    {
        private int _released;
        private readonly WeakReference<GatedDataSource> _source = new(source);

        internal bool IsCurrent => Volatile.Read(ref _released) == 0;

        internal bool OriginatesFrom(GatedDataSource source) =>
            _source.TryGetTarget(out var owner) && ReferenceEquals(owner, source);

        internal OrmDiagnostics Diagnostics { get; } = diagnostics;

        internal long ConnectionId { get; } = diagnostics.Active ? OrmDiagnostics.NextId() : 0;

        internal TransactionTrace? Transaction { get; set; }

        public void Acquire() => scope?.Acquire();

        internal void ReleaseStream()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) scope?.Release();
        }

        public void OnStateChange(object sender, StateChangeEventArgs e)
        {
            if (e.CurrentState is ConnectionState.Closed or ConnectionState.Broken)
            {
                if (Interlocked.Exchange(ref _released, 1) == 0) scope?.Release();
                return;
            }

            if (e.CurrentState != ConnectionState.Open || Volatile.Read(ref _released) == 0) return;
            throw new InvalidOperationException(
                "GatedDataSource refuses to re-open a connection it has already leased and released: "
                +
            "use OpenConnectionAsync or CreateCommand on the data source, which lease through LeaseAsync, the only leasing entry point."
            );
        }
    }

    internal sealed class GatedCommand : DbCommand
    {
        internal StatementKind Kind { get; set; } = StatementKind.Read;

        private readonly GatedDataSource _gate;
        private readonly DbConnection _connection;
        private readonly DbCommand _inner;

        public GatedCommand(GatedDataSource gate, DbConnection connection, string? commandText)
        {
            _gate = gate;
            _connection = connection;
            try
            {
                _inner = connection.CreateCommand();
                if (commandText is not null) _inner.CommandText = commandText;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        [AllowNull]
        public override string CommandText
        {
            get => _inner.CommandText;
            set => _inner.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _inner.CommandTimeout;
            set => _inner.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _inner.CommandType;
            set => _inner.CommandType = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _inner.UpdatedRowSource;
            set => _inner.UpdatedRowSource = value;
        }

        public override bool DesignTimeVisible
        {
            get => _inner.DesignTimeVisible;
            set => _inner.DesignTimeVisible = value;
        }

        protected override DbConnection? DbConnection
        {
            get => null;
            set => throw Refused("setting Connection on a data-source command",
            "OpenConnectionAsync and that connection's CreateCommand");
        }

        protected override DbTransaction? DbTransaction
        {
            get => null;
            set => throw Refused("setting Transaction on a data-source command",
            "OpenConnectionAsync and that connection's CreateCommand");
        }

        protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

        protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

        public override void Cancel() => _inner.Cancel();

        public override void Prepare() => throw Refused("Prepare", "an Execute*Async member");

        public override int ExecuteNonQuery() => throw Refused("ExecuteNonQuery", "ExecuteNonQueryAsync");

        public override object? ExecuteScalar() => throw Refused("ExecuteScalar", "ExecuteScalarAsync");

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            throw Refused("ExecuteReader", "ExecuteReaderAsync");

        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            await _gate.LeaseAsync(_connection, cancellationToken);
            try
            {
                return await _inner.RunNonQueryAsync(Kind, cancellationToken);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public override async Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            await _gate.LeaseAsync(_connection, cancellationToken);
            try
            {
                return await _inner.RunScalarAsync(Kind, cancellationToken);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior, CancellationToken cancellationToken)
        {
            await _gate.LeaseAsync(_connection, cancellationToken);
            try
            {
                return await _inner.RunReaderAsync(Kind, cancellationToken, behavior | CommandBehavior.CloseConnection);
            }
            catch
            {
                await _connection.CloseAsync();
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _connection.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            await _connection.DisposeAsync();
            base.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
