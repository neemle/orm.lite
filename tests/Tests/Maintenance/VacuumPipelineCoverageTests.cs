using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Neemle.OrmLite.Tests.Nodes;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// ORML-134 fix pass (ADR e8c50bcc v5 D12.3, D12.9; Stage-3 F4): the spent budget, the guard teardown, the contention
// predicates and the PostgreSQL writer key, on the edges the AC tests do not reach.
public sealed class VacuumPipelineCoverageTests
{
    [Theory(DisplayName = "ORML-134 D12.3 a budget spent before the gate or before the open returns Busy without opening a connection")]
    [InlineData(WalCheckpointMode.Passive)]
    [InlineData(WalCheckpointMode.Truncate)]
    public async Task A_spent_budget_opens_nothing(WalCheckpointMode mode)
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        var (context, source) = db.Counting();
        db.Sink.Clear();

        var result = await context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(mode) { Timeout = TimeSpan.FromTicks(1) });

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Null(result.WalFrames);
        Assert.Equal(0, source.Opens);
        Assert.Empty(db.Sink.Maintenance);
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
    }

    [Fact(DisplayName = "ORML-134 a guard teardown that fails is swallowed: the call still completes and reports one event per statement")]
    public async Task A_failing_teardown_is_swallowed()
    {
        await using var db = await WalFixture.WithItemsAsync(5);
        var provider = new TunedSqliteProvider(SqliteProvider.Instance.Capabilities)
        {
            Guard = new MaintenanceSessionGuard("SELECT 1", "THIS IS NOT SQL"),
        };
        var (context, _) = db.Counting(provider);
        db.Sink.Clear();

        var result = await context.Maintenance.VacuumAsync(VacuumFixture.Plain(5000));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.Equal([nameof(MaintenanceOperation.WalCheckpoint), nameof(MaintenanceOperation.Vacuum)], db.Sink.Operations);
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
    }

    [Theory(DisplayName = "ORML-134 SQLite contention is SQLITE_BUSY or SQLITE_LOCKED, extended codes included, and only a SqliteException")]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(261, true)]
    [InlineData(262, true)]
    [InlineData(1, false)]
    [InlineData(19, false)]
    public void Sqlite_contention(int code, bool contention) =>
        Assert.Equal(contention, SqliteProvider.Instance.IsContention(new SqliteException("probe", code)));

    [Fact(DisplayName = "ORML-134 a DbException of another driver is never contention, on either provider")]
    public void Foreign_exceptions_are_not_contention()
    {
        Assert.False(SqliteProvider.Instance.IsContention(new ForeignDbException()));
        Assert.False(PostgresProvider.Instance.IsContention(new ForeignDbException()));
        Assert.False(PostgresProvider.Instance.IsContention(new SqliteException("busy", 5)));
    }

    [Fact(DisplayName = "ORML-134 D12.9 the PostgreSQL writer key lower-cases the host, keeps the database's case, and tolerates no host")]
    public void Postgres_writer_key()
    {
        using var mixed = NpgsqlDataSource.Create("Host=Db.Example.Invalid;Port=6543;Database=MixedCase;Username=u;Password=secret");

        Assert.Equal("postgres:db.example.invalid:6543/MixedCase", PostgresProvider.Instance.WriterKeyOf(mixed));
        Assert.Equal("postgres::5432/only", PostgresProvider.Instance.WriterKeyOf(new ConnectionStringOnly("Database=only")));
    }

    [Fact(DisplayName = "ORML-134 D12.9 the guard's statements name the lock_timeout setting the DDL path uses")]
    public void The_guard_uses_the_shared_setting_name()
    {
        var guard = PostgresProvider.Instance.RenderLockGuard(TimeSpan.FromSeconds(2))!;

        Assert.StartsWith("SET lock_timeout ", guard.Setup, StringComparison.Ordinal);
        Assert.Equal("RESET lock_timeout", guard.Teardown);
    }

    // Stage-3 F4: with the driver's reset on close switched off and a pool of one, the next lease of the same physical
    // connection sees the server's lock_timeout again: the guard's RESET ran. The library's own GatedDataSource refuses
    // No Reset On Close, so the context is built over the driver's data source directly.
    [PostgresFact(DisplayName = "ORML-134 Stage-3 F4 the lock guard's RESET runs on the maintenance connection itself (No Reset On Close, pool of 1)")]
    public async Task The_reset_runs_on_the_connection()
    {
        await using var db = await PostgresMaintenanceDatabase.CreateAsync();
        var serverDefault = Convert.ToString(await db.ScalarAsync("SHOW lock_timeout"), System.Globalization.CultureInfo.InvariantCulture);
        var connectionString = new NpgsqlConnectionStringBuilder(db.ConnectionString) { MaxPoolSize = 1, NoResetOnClose = true }.ConnectionString;
        await using var source = NpgsqlDataSource.Create(connectionString);
        await using var context = new OrmContext(source, PostgresProvider.Instance);

        var before = await ScalarAsync(source, "SELECT pg_backend_pid()");
        foreach (var call in new Func<Task<IMaintenanceResult>>[]
        {
            async () => await context.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain) { Timeout = TimeSpan.FromSeconds(7) }),
            async () => await context.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database) { Timeout = TimeSpan.FromSeconds(7) }),
        })
        {
            Assert.Equal(MaintenanceStatus.Completed, (await call()).Status);
            Assert.Equal(before, await ScalarAsync(source, "SELECT pg_backend_pid()"));
            Assert.Equal(serverDefault, await ScalarAsync(source, "SHOW lock_timeout"));
        }
        Assert.NotEqual("7s", serverDefault);
    }

    private static async Task<string?> ScalarAsync(DbDataSource source, string sql)
    {
        await using var connection = await source.OpenConnectionAsync();
        return Convert.ToString(await SqliteMaintenanceDatabase.Scalar(connection, sql), System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class ForeignDbException : DbException;

    private sealed class ConnectionStringOnly(string connectionString) : DbDataSource
    {
        public override string ConnectionString => connectionString;

        protected override DbConnection CreateDbConnection() => throw new NotSupportedException();
    }
}
