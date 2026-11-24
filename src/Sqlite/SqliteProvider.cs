using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sqlite;

public sealed class SqliteProvider(TimeSpan busyTimeout, ISqliteTriggerRegenerator? triggers = null) : ISqlProvider
{
    public const int BindParameterLimit = 32766;

    public static readonly Version MinimumVersion = new(3, 53, 3);

    public static readonly TimeSpan DefaultBusyTimeout = TimeSpan.FromSeconds(30);

    public static readonly SqliteProvider Instance = new(DefaultBusyTimeout);

    public string Id => "sqlite";

    public CapabilitySet Capabilities { get; } = CapabilitySet.Everything()
        .With(Capability.ArrayParameter, Support.Emulated)
        .With(Capability.RowLock, Support.Emulated)
        .With(Capability.KeyPreservingRowLock, Support.Emulated)
        .With(Capability.IntervalArithmetic, Support.Emulated)
        .With(Capability.NamedLock, Support.Emulated)
        .With(Capability.JsonDocument, Support.Emulated)
        .With(Capability.SessionLock, Support.None)
        .With(Capability.AlterConstraint, Support.Emulated)
        .With(Capability.ObjectMarker, Support.Emulated)
        .With(Capability.RowHistoryTriggers, Support.Emulated)
        .With(Capability.CatalogGuard, Support.Emulated)
        .With(Capability.ExactDecimal, Support.Emulated)
        .With(Capability.IndexNullsOrdering, Support.None);

    public int MaxBindParameters => BindParameterLimit;

    private IMigrationPlanner? _planner;

    public IMigrationPlanner Planner => _planner ??= new SqliteMigrationPlanner(this);

    internal ISqliteTriggerRegenerator Triggers => triggers ?? SqliteHistoryTriggers.Instance;

    public Node Lower(Node tree) => SqliteLowering.Lower(tree);

    public ISchemaCatalog CreateCatalog(DbConnection connection, DbTransaction? transaction = null) =>
        new SqliteCatalog(this, connection, transaction);

    public TimeSpan BusyTimeout => busyTimeout;

    public ISerializer CreateSerializer() => new SqliteSerializer(MaxBindParameters);

    public void ValidateDataSource(string name, DbDataSource source)
    {
        SqliteDataSourceProbe.Require(name, source);
        if (string.IsNullOrWhiteSpace(source.ConnectionString)) return;
        var builder = new SqliteConnectionStringBuilder(source.ConnectionString);
        if (builder.DefaultTimeout <= 0)
            throw new InvalidOperationException(
                $"the sqlite data source for the {name} lane sets Default Timeout to {builder.DefaultTimeout}, "
                + "and a timeout of 0 makes SQLite wait forever on a busy database. "
                + "Set a finite number of seconds.");
    }

    public Task StampActorAsync(DbConnection connection, DbActorValue actor, CancellationToken ct)
    {
        ((OrmSqliteConnection)connection).Actor = actor.Stamp;
        return Task.CompletedTask;
    }

    public Task ClearActorAsync(DbConnection connection, CancellationToken ct)
    {
        ((OrmSqliteConnection)connection).Actor = null;
        return Task.CompletedTask;
    }

    public async ValueTask<ProviderTransaction> BeginTransactionAsync(
        DbConnection connection, DbDataSource source, CancellationToken ct = default)
    {
        var gate = await EnterWriteAsync(source, ct);
        try
        {
            return new ProviderTransaction(((OrmSqliteConnection)connection).BeginWriteTransaction(), gate);
        }
        catch
        {
            if (gate is not null) await gate.DisposeAsync();
            throw;
        }
    }

    public ValueTask<IAsyncDisposable?> EnterWriteAsync(DbDataSource source, CancellationToken ct = default) =>
        SqliteWriterGate.EnterAsync(SqliteDatabaseKey.Of(source), busyTimeout, ct);
}
