using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sqlite;

namespace Neemle.OrmLite.Tests.Maintenance;

// Records every statement event of one context; maintenance assertions read the Maintenance ones.
internal sealed class MaintenanceSink : IOrmDiagnosticsSink
{
    private readonly ConcurrentQueue<StatementEvent> _statements = new();

    public void OnStatement(StatementEvent statement) => _statements.Enqueue(statement);

    public void OnTransaction(TransactionEvent transaction)
    {
    }

    internal IReadOnlyList<StatementEvent> Maintenance =>
        _statements.Where(s => s.Kind == StatementKind.Maintenance).ToList();

    internal IReadOnlyList<string?> Operations => Maintenance.Select(s => s.Operation).ToList();

    internal void Clear() => _statements.Clear();
}

// Counts every connection the maintenance call creates or opens through it; the connections themselves come from the
// wrapped source, so lanes, leases and diagnostics behave as without the wrapper.
internal sealed class OpenCountingDataSource(DbDataSource inner) : DbDataSource
{
    private int _opens;

    internal int Opens => Volatile.Read(ref _opens);

    public override string ConnectionString => inner.ConnectionString;

    protected override DbConnection CreateDbConnection()
    {
        Interlocked.Increment(ref _opens);
        return inner.CreateConnection();
    }

    protected override async ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _opens);
        return await inner.OpenConnectionAsync(cancellationToken);
    }
}

// A provider that knows nothing of maintenance: the core must refuse it by capability before any connection opens.
internal sealed class PortlessProvider : ISqlProvider
{
    public string Id => "portless";

    public CapabilitySet Capabilities { get; } = CapabilitySet.Everything();

    public ISerializer CreateSerializer() => throw new NotSupportedException("the portless provider renders nothing.");
}

// SQLite's maintenance port with one capability switched off: proves the core reads the declared level. Each item's
// test file adds the members of its own operation.
internal sealed partial class TunedSqliteProvider(CapabilitySet capabilities) : ISqlProvider, IMaintenanceProvider
{
    private static readonly SqliteProvider Inner = SqliteProvider.Instance;

    internal static TunedSqliteProvider Without(Capability capability) =>
        new(Inner.Capabilities.With(capability, Support.None));

    public string Id => "tuned-sqlite";

    public CapabilitySet Capabilities { get; } = capabilities;

    public ISerializer CreateSerializer() => Inner.CreateSerializer();

    public TimeSpan MaintenanceTimeout => Inner.MaintenanceTimeout;

    public string? WriterKeyOf(DbDataSource source) => Inner.WriterKeyOf(source);

    public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
        Inner.TryEnterWriteAsync(source, timeout, ct);

    public string RenderWalCheckpoint(WalCheckpointMode mode) => Inner.RenderWalCheckpoint(mode);
}

// Runs an action, in the flow that set it, when the library starts a statement activity: the statement's gate (if any) is
// already held, and the statement has not run yet. Blocking in the action holds the statement there.
internal sealed class StatementStartHook : IDisposable
{
    private static readonly AsyncLocal<Action?> Action = new();

    private readonly ActivityListener _listener;

    internal StatementStartHook()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Neemle.OrmLite",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = _ => Action.Value?.Invoke(),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    // Must be called in the async method that then awaits the statement, so the action flows into it.
    internal static void OnStart(Action? action) => Action.Value = action;

    public void Dispose() => _listener.Dispose();
}

// A SQLite file database for one test, opened through OrmLite.OpenAsync with a recording sink.
internal sealed class SqliteMaintenanceDatabase : IAsyncDisposable
{
    internal static readonly TimeSpan Margin = TimeSpan.FromSeconds(2);

    private readonly SqliteConnection? _keeper;

    private SqliteMaintenanceDatabase(string directory, string path, string raw, MaintenanceSink sink, OrmContext context,
        SqliteConnection? keeper)
    {
        Directory = directory;
        Path = path;
        RawConnectionString = raw;
        Sink = sink;
        Context = context;
        _keeper = keeper;
    }

    internal string Directory { get; }

    internal string Path { get; }

    internal string RawConnectionString { get; }

    internal MaintenanceSink Sink { get; }

    internal OrmContext Context { get; }

    internal OrmMaintenance Maintenance => Context.Maintenance;

    internal string WalPath => Path + "-wal";

    // A WAL file database, as every file database opened through OrmLite.OpenAsync is. One idle connection stays open for
    // the test's lifetime: when the last connection to a WAL database closes, SQLite checkpoints and deletes the log,
    // which would hide the frames the tests checkpoint.
    internal static async Task<SqliteMaintenanceDatabase> OpenWalAsync(int? busyTimeoutSeconds = null)
    {
        var directory = NewDirectory();
        var path = System.IO.Path.Combine(directory, "maint.db");
        var raw = "Data Source=" + path + ";Pooling=False";
        var sink = new MaintenanceSink();
        var context = await OrmLite.OpenAsync(Options(sink, o => { o.Path = path; o.BusyTimeoutSeconds = busyTimeoutSeconds; }));
        // The pooled connection that switched the new file to WAL reports busy on its first TRUNCATE checkpoint while
        // another connection holds the log open (SQLite 3.53, Microsoft.Data.Sqlite pooling); close it so every test
        // starts from connections opened in WAL mode.
        SqliteConnection.ClearAllPools();
        var keeper = new SqliteConnection(raw);
        await keeper.OpenAsync();
        // A connection holds the log open only once it has read the database.
        await Scalar(keeper, "SELECT count(*) FROM sqlite_master");
        return new SqliteMaintenanceDatabase(directory, path, raw, sink, context, keeper);
    }

    // A file database in rollback-journal mode, still behind the library's gated data source.
    internal static async Task<SqliteMaintenanceDatabase> OpenRollbackJournalAsync(int? busyTimeoutSeconds = null)
    {
        var directory = NewDirectory();
        var path = System.IO.Path.Combine(directory, "journal.db");
        var raw = "Data Source=" + path + ";Pooling=False";
        await using (var create = new SqliteConnection(raw))
        {
            await create.OpenAsync();
            await Exec(create, "PRAGMA journal_mode=DELETE");
            await Exec(create, "CREATE TABLE IF NOT EXISTS seed (id INTEGER PRIMARY KEY)");
        }
        var sink = new MaintenanceSink();
        var context = await OrmLite.OpenAsync(Options(sink, o => { o.Path = path; o.BusyTimeoutSeconds = busyTimeoutSeconds; }));
        SqliteConnection.ClearAllPools();
        await using (var back = new SqliteConnection(raw))
        {
            await back.OpenAsync();
            var mode = await Scalar(back, "PRAGMA journal_mode=DELETE");
            if (!string.Equals(Convert.ToString(mode), "delete", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("the fixture could not switch the database back to a rollback journal: " + mode);
        }
        return new SqliteMaintenanceDatabase(directory, path, raw, sink, context, null);
    }

    // A shared-cache in-memory database, kept alive by one open connection for the test's lifetime.
    internal static async Task<SqliteMaintenanceDatabase> OpenMemoryAsync()
    {
        var name = "maint-" + Guid.NewGuid().ToString("N");
        var raw = "Data Source=" + name + ";Mode=Memory;Cache=Shared";
        var keeper = new SqliteConnection(raw);
        await keeper.OpenAsync();
        var sink = new MaintenanceSink();
        var context = await OrmLite.OpenAsync(Options(sink, o => o.ConnectionString = raw));
        return new SqliteMaintenanceDatabase("", "", raw, sink, context, keeper);
    }

    private static OrmLiteOptions Options(MaintenanceSink sink, Action<OrmLiteOptions> configure)
    {
        SqliteProviderFactory.Register();
        var options = new OrmLiteOptions { Provider = "sqlite" };
        configure(options);
        options.Diagnostics.Sink = sink;
        options.Diagnostics.StatementLevel = OrmLogLevel.Information;
        return options;
    }

    private static string NewDirectory()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ormlite-maint-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        return directory;
    }

    internal async Task<SqliteConnection> RawAsync()
    {
        var connection = new SqliteConnection(RawConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    internal async Task ExecAsync(string sql)
    {
        await using var connection = await RawAsync();
        await Exec(connection, sql);
    }

    internal async Task<long> LongAsync(string sql)
    {
        await using var connection = await RawAsync();
        return Convert.ToInt64(await Scalar(connection, sql), System.Globalization.CultureInfo.InvariantCulture);
    }

    internal async Task<List<string>> DumpAsync(string sql)
    {
        await using var connection = await RawAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            var cells = new object?[reader.FieldCount];
            for (var i = 0; i < cells.Length; i++) cells[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(string.Join("|", cells.Select(c => Convert.ToString(c, System.Globalization.CultureInfo.InvariantCulture))));
        }
        return rows;
    }

    internal static async Task Exec(DbConnection connection, string sql, DbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task<object?> Scalar(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    // A context over the same gated source, counting every connection a maintenance call opens.
    internal (OrmContext Context, OpenCountingDataSource Source) Counting(ISqlProvider? provider = null)
    {
        var source = new OpenCountingDataSource(Context.DataSource);
        return (new OrmContext(source, provider ?? Context.Provider), source);
    }

    // One ORM write: a unit of work (which takes the writer gate) that inserts a row and commits.
    internal async Task WriteAsync(string table = "items", string value = "w")
    {
        await using var unit = await UnitOfWork.BeginAsync(Context);
        await Exec(unit.Connection, "INSERT INTO " + table + " (v) VALUES ('" + value + "')", unit.Transaction);
        await unit.CommitAsync();
    }

    internal string FileHash() => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path)));

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        if (_keeper is not null) await _keeper.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Length > 0 && System.IO.Directory.Exists(Directory))
        {
            try { System.IO.Directory.Delete(Directory, true); }
            catch (IOException) { }
        }
    }
}

// Holds a database's writer gate from another flow through an open unit of work until released.
internal sealed class GateHolder : IAsyncDisposable
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _run;

    private GateHolder(OrmContext context, string? write)
    {
        // Task.Run copies the caller's execution context as it is NOW, and the unit of work's flow mark is installed
        // inside the task, so the caller's flow never sees it: this is "another flow".
        _run = Task.Run(async () =>
        {
            await using var unit = await UnitOfWork.BeginAsync(context);
            if (write is not null) await SqliteMaintenanceDatabase.Exec(unit.Connection, write, unit.Transaction);
            _held.SetResult();
            await _release.Task;
            await unit.CommitAsync();
        });
    }

    internal static async Task<GateHolder> HoldAsync(OrmContext context, string? write = null)
    {
        var holder = new GateHolder(context, write);
        await Task.WhenAny(holder._held.Task, holder._run);
        if (holder._run.IsFaulted) await holder._run;
        return holder;
    }

    internal async Task CommitAsync()
    {
        _release.TrySetResult();
        await _run;
    }

    public async ValueTask DisposeAsync()
    {
        _release.TrySetResult();
        try { await _run; }
        catch { }
    }
}

internal static class Descriptors
{
    internal static EntityDescriptor Table(string name) => EntityDescriptor.Of("Fixture" + name, name, true);

    internal static string Quoted(ISqlProvider provider, string table) =>
        provider.CreateSerializer().Fragment(new TableRef(table));
}
