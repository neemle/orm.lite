#nullable enable
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

public sealed class GatedDataSource(DbDataSource request, DbDataSource background, DbDataSource ops) : DbDataSource
{
    private static readonly ConditionalWeakTable<DbConnection, Lease> s_leases = new();

    private readonly DbDataSource _request = Checked(request, nameof(request));
    private readonly DbDataSource _background = Checked(background, nameof(background));
    private readonly DbDataSource _ops = Checked(ops, nameof(ops));

    public override string ConnectionString => _background.ConnectionString;

    private static DbDataSource Checked(DbDataSource? source, string name)
    {
        if (source is null) throw new ArgumentNullException(name);
        if (SetsNoResetOnClose(source.ConnectionString))
            throw new InvalidOperationException(
                $"GatedDataSource refuses the {name} data source: its connection string sets No Reset On Close, "
                + "which would let a pooled connection keep the previous caller's "
                + OrmLiteSettings.ActorSetting + ". Remove No Reset On Close from the connection string.");
        return source;
    }

    private static bool SetsNoResetOnClose(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        var builder = new DbConnectionStringBuilder();
        try
        {
            builder.ConnectionString = connectionString;
        }
        catch (ArgumentException)
        {
            return false;
        }

        foreach (string key in builder.Keys)
        {
            var normalised = key.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            if (normalised != "noresetonclose") continue;
            var value = Convert.ToString(builder[key], CultureInfo.InvariantCulture)?.Trim().ToLowerInvariant();
            if (value is not ("false" or "no" or "off" or "0")) return true;
        }
        return false;
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
        var lease = new Lease(DbLane.CurrentScope);
        lease.Acquire();
        s_leases.Add(inner, lease);
        inner.StateChange += lease.OnStateChange;

        if (DbActor.Current is { } actor)
        {
            try
            {
                await DbActor.StampAsync(inner, actor, ct);
            }
            catch
            {
                await inner.CloseAsync();
                throw;
            }
        }
        return inner;
    }

    protected override async ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken cancellationToken = default)
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

    protected override DbCommand CreateDbCommand(string? commandText = null) =>
        new GatedCommand(this, LaneSource(DbLane.Current).CreateConnection(), commandText);

    protected override DbConnection CreateDbConnection() =>
        throw Refused("CreateConnection", "OpenConnectionAsync");

    protected override DbConnection OpenDbConnection() =>
        throw Refused("OpenConnection", "OpenConnectionAsync");

    protected override DbBatch CreateDbBatch() =>
        throw Refused("CreateBatch", "CreateCommand and ExecuteNonQueryAsync, or a batch on a connection from OpenConnectionAsync");

    private static NotSupportedException Refused(string member, string instead) =>
        new($"GatedDataSource refuses {member}: every connection is leased through the gate, so use {instead} instead.");

    private sealed class Lease(DbLaneScope? scope)
    {
        private int _released;

        public void Acquire() => scope?.Acquire();

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
                + "use OpenConnectionAsync or CreateCommand on the data source, which lease through LeaseAsync, the only leasing entry point.");
        }
    }

    private sealed class GatedCommand : DbCommand
    {
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
            set => throw Refused("setting Connection on a data-source command", "OpenConnectionAsync and that connection's CreateCommand");
        }

        protected override DbTransaction? DbTransaction
        {
            get => null;
            set => throw Refused("setting Transaction on a data-source command", "OpenConnectionAsync and that connection's CreateCommand");
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
                return await _inner.ExecuteNonQueryAsync(cancellationToken);
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
                return await _inner.ExecuteScalarAsync(cancellationToken);
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
                return await _inner.ExecuteReaderAsync(behavior | CommandBehavior.CloseConnection, cancellationToken);
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
