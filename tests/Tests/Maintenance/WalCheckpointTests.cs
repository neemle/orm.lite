using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

internal static class WalFixture
{
    internal const string Items = "CREATE TABLE IF NOT EXISTS items (id INTEGER PRIMARY KEY, v TEXT NOT NULL)";

    internal static async Task<SqliteMaintenanceDatabase> WithItemsAsync(int rows = 20)
    {
        var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(Items);
        for (var i = 0; i < rows; i++) await db.ExecAsync("INSERT INTO items (v) VALUES ('row " + i + "')");
        return db;
    }

    internal static WalCheckpointRequest Request(WalCheckpointMode mode, int? timeoutMs = null) =>
        new(mode) { Timeout = timeoutMs is { } ms ? TimeSpan.FromMilliseconds(ms) : null };
}

// AC-137-01: four explicit modes, rendered from the enum only, never escalated or downgraded; no zero member.
public sealed class WalCheckpointModeTests
{
    [Theory(DisplayName = "AC-137-01 each checkpoint mode renders exactly its own SQLite mode")]
    [InlineData(WalCheckpointMode.Passive, "PRAGMA wal_checkpoint(PASSIVE)")]
    [InlineData(WalCheckpointMode.Full, "PRAGMA wal_checkpoint(FULL)")]
    [InlineData(WalCheckpointMode.Restart, "PRAGMA wal_checkpoint(RESTART)")]
    [InlineData(WalCheckpointMode.Truncate, "PRAGMA wal_checkpoint(TRUNCATE)")]
    public async Task Each_mode_runs_exactly_its_statement(WalCheckpointMode mode, string expected)
    {
        Assert.Equal(expected, SqliteProvider.Instance.RenderWalCheckpoint(mode));
        await using var db = await WalFixture.WithItemsAsync(1);

        var result = await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(mode));

        Assert.Equal(mode, result.Mode);
        Assert.Equal(expected, Assert.Single(db.Sink.Maintenance).Text);
    }

    [Fact(DisplayName = "AC-137-01 the mode enum has the four modes and no zero member")]
    public void The_modes_are_explicit()
    {
        Assert.Equal([1, 2, 3, 4], Enum.GetValues<WalCheckpointMode>().Select(m => (int)m));
        Assert.False(Enum.IsDefined((WalCheckpointMode)0));
    }

    [Theory(DisplayName = "AC-137-01 a zero or undefined mode, a null request and a non-positive timeout are refused before any connection")]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Undefined_modes_are_refused_before_io(int mode)
    {
        var context = new OrmContext(new NeverUsedDataSource(), SqliteProvider.Instance);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest((WalCheckpointMode)mode)));
        await Assert.ThrowsAsync<ArgumentNullException>(() => context.Maintenance.CheckpointWalAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Passive) { Timeout = TimeSpan.Zero }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Passive) { Timeout = TimeSpan.FromSeconds(-1) }));
    }

    [Fact(DisplayName = "AC-137-01 validation comes before the capability check")]
    public async Task Validation_precedes_the_capability_check()
    {
        var context = new OrmContext(new NeverUsedDataSource(), new PortlessProvider());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest((WalCheckpointMode)0)));
        await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Passive)));
    }
}

// AC-137-02 and AC-137-03: a structured result from SQLite's busy/log/checkpointed row; a successful checkpoint.
public sealed class WalCheckpointCompletionTests
{
    [Fact(DisplayName = "AC-137-03 Passive on a WAL file database with committed writes completes with frames equal to checkpointed")]
    public async Task Passive_completes()
    {
        await using var db = await WalFixture.WithItemsAsync();

        var result = await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Passive));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.True(result.IsComplete);
        Assert.NotNull(result.WalFrames);
        Assert.True(result.WalFrames > 0);
        Assert.Equal(result.WalFrames, result.CheckpointedFrames);
        Assert.Equal(MaintenanceOperation.WalCheckpoint, result.Operation);
        Assert.Equal("sqlite", result.Provider);
    }

    [Fact(DisplayName = "AC-137-03 Truncate completes, leaves a 0-byte -wal file and a fresh connection reads every committed row")]
    public async Task Truncate_empties_the_log()
    {
        await using var db = await WalFixture.WithItemsAsync(25);
        Assert.True(new FileInfo(db.WalPath).Length > 0);

        var result = await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.Equal(0L, result.WalFrames);
        Assert.Equal(0L, result.CheckpointedFrames);
        Assert.Equal(0L, new FileInfo(db.WalPath).Length);
        Assert.Equal(25L, await db.LongAsync("SELECT count(*) FROM items"));
    }

    [Fact(DisplayName = "AC-137-02 Full and Restart report the row SQLite returned and complete on an idle database")]
    public async Task Full_and_restart_complete()
    {
        await using var db = await WalFixture.WithItemsAsync();

        var full = await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Full));
        await db.ExecAsync("INSERT INTO items (v) VALUES ('after full')");
        var restart = await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Restart));

        Assert.Equal(MaintenanceStatus.Completed, full.Status);
        Assert.Equal(full.WalFrames, full.CheckpointedFrames);
        Assert.Equal(MaintenanceStatus.Completed, restart.Status);
        Assert.Equal(restart.WalFrames, restart.CheckpointedFrames);
    }

    [Fact(DisplayName = "AC-137-02 a native failure that is not contention propagates as the original DbException")]
    public async Task A_native_failure_propagates()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ormlite-maint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "garbage.db");
        await File.WriteAllTextAsync(path, new string('x', 8192));
        try
        {
            var context = new OrmContext(SqliteFactory.Instance.CreateDataSource("Data Source=" + path + ";Pooling=False"), SqliteProvider.Instance);

            var failure = await Assert.ThrowsAsync<SqliteException>(() => context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate)));

            Assert.Equal(26, failure.SqliteErrorCode);
            // The gate was released: a gate-taking checkpoint of a sound database at the same path proceeds.
            File.Delete(path);
            var result = await context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate) { Timeout = TimeSpan.FromSeconds(1) });
            Assert.NotEqual(MaintenanceStatus.Busy, result.Status);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }
}

// AC-137-04: an open read snapshot followed by newer commits.
public sealed class WalCheckpointActiveReaderTests
{
    [Fact(DisplayName = "AC-137-04 with an open reader Passive is Partial without waiting, Truncate is Busy within the timeout plus 2 s, the reader is unaffected, and Truncate completes after the reader ends")]
    public async Task A_reader_holds_back_the_checkpoint()
    {
        await using var db = await WalFixture.WithItemsAsync(10);
        await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate));

        await using var reader = await db.RawAsync();
        await using var snapshot = reader.BeginTransaction(deferred: true);
        var seen = await CountAsync(reader, snapshot);
        for (var i = 0; i < 10; i++) await db.ExecAsync("INSERT INTO items (v) VALUES ('newer " + i + "')");

        var passiveClock = Stopwatch.StartNew();
        var passive = await db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Passive, 5000));
        passiveClock.Stop();
        var truncateClock = Stopwatch.StartNew();
        var truncate = await db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 1000));
        truncateClock.Stop();

        Assert.Equal(MaintenanceStatus.Partial, passive.Status);
        Assert.True(passive.CheckpointedFrames < passive.WalFrames);
        Assert.True(passiveClock.Elapsed < TimeSpan.FromSeconds(2), "Passive waited " + passiveClock.Elapsed);
        Assert.Equal(MaintenanceStatus.Busy, truncate.Status);
        Assert.False(truncate.IsComplete);
        Assert.True(truncateClock.Elapsed < TimeSpan.FromSeconds(1) + SqliteMaintenanceDatabase.Margin, "Truncate took " + truncateClock.Elapsed);
        Assert.Equal(seen, await CountAsync(reader, snapshot));

        await snapshot.CommitAsync();
        var after = await db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000));
        Assert.Equal(MaintenanceStatus.Completed, after.Status);
        Assert.Equal(0L, new FileInfo(db.WalPath).Length);
    }

    private static async Task<long> CountAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT count(*) FROM items";
        return (long)(await command.ExecuteScalarAsync())!;
    }
}

// AC-137-05: a database that is not in WAL mode.
public sealed class WalCheckpointNotWalTests
{
    [Theory(DisplayName = "AC-137-05 a rollback-journal file database reports NotWal in every mode and the file is byte-identical")]
    [InlineData(WalCheckpointMode.Passive)]
    [InlineData(WalCheckpointMode.Truncate)]
    public async Task A_rollback_journal_is_not_wal(WalCheckpointMode mode)
    {
        await using var db = await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        await db.ExecAsync("INSERT INTO seed DEFAULT VALUES");
        SqliteConnection.ClearAllPools();
        var before = db.FileHash();

        var result = await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(mode));

        Assert.Equal(MaintenanceStatus.NotWal, result.Status);
        Assert.Null(result.WalFrames);
        Assert.Null(result.CheckpointedFrames);
        Assert.False(result.IsComplete);
        SqliteConnection.ClearAllPools();
        Assert.Equal(before, db.FileHash());
        Assert.False(File.Exists(db.WalPath));
    }

    [Fact(DisplayName = "AC-137-05 an in-memory database reports NotWal")]
    public async Task An_in_memory_database_is_not_wal()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenMemoryAsync();

        var result = await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate));

        Assert.Equal(MaintenanceStatus.NotWal, result.Status);
        Assert.Null(result.WalFrames);
    }
}

// AC-137-06: the Ops lane, no transaction, the writer gate and the holder refusal.
public sealed class WalCheckpointConcurrencyTests
{
    [Fact(DisplayName = "AC-137-06 the checkpoint runs on an Ops-lane connection outside any transaction")]
    public async Task Runs_on_the_ops_lane_without_a_transaction()
    {
        await using var db = await WalFixture.WithItemsAsync(3);
        using var hook = new StatementStartHook();
        DbLaneKind? lane = null;
        StatementStartHook.OnStart(() => lane = DbLane.Current);

        await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate));
        StatementStartHook.OnStart(null);

        Assert.Equal(DbLaneKind.Ops, lane);
        Assert.Null(Assert.Single(db.Sink.Maintenance).TransactionId);
    }

    [Theory(DisplayName = "AC-137-06 a gate-taking mode returns Busy when another flow's unit of work holds the gate past the timeout, with no connection opened")]
    [InlineData(WalCheckpointMode.Full)]
    [InlineData(WalCheckpointMode.Restart)]
    [InlineData(WalCheckpointMode.Truncate)]
    public async Task A_busy_gate_is_a_busy_result(WalCheckpointMode mode)
    {
        await using var db = await WalFixture.WithItemsAsync(2);
        var (context, source) = db.Counting();
        await using var holder = await GateHolder.HoldAsync(db.Context);

        var clock = Stopwatch.StartNew();
        var result = await context.Maintenance.CheckpointWalAsync(WalFixture.Request(mode, 200));
        clock.Stop();

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Null(result.WalFrames);
        Assert.Null(result.CheckpointedFrames);
        Assert.Equal(0, source.Opens);
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(200) + SqliteMaintenanceDatabase.Margin);
        Assert.Empty(db.Sink.Maintenance);
    }

    [Fact(DisplayName = "AC-137-06 Passive takes no gate: it completes while another flow holds the gate")]
    public async Task Passive_does_not_wait_for_the_gate()
    {
        await using var db = await WalFixture.WithItemsAsync(2);
        await using var holder = await GateHolder.HoldAsync(db.Context);

        var result = await db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Passive, 200));

        Assert.NotEqual(MaintenanceStatus.Busy, result.Status);
        Assert.NotNull(result.WalFrames);
    }

    [Fact(DisplayName = "AC-137-06 an ORM write on the same database waits for a gate-taking checkpoint and then completes; a write on another file is not delayed")]
    public async Task Writes_wait_on_the_same_database_only()
    {
        await using var db = await WalFixture.WithItemsAsync(2);
        await using var other = await WalFixture.WithItemsAsync(0);
        using var hook = new StatementStartHook();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var checkpoint = Task.Run(async () =>
        {
            StatementStartHook.OnStart(() => { started.Set(); release.Wait(TimeSpan.FromSeconds(20)); });
            return await db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 20000));
        });
        Assert.True(started.Wait(TimeSpan.FromSeconds(10)));

        var sameDatabase = Task.Run(() => db.WriteAsync());
        var otherDatabase = Task.Run(() => other.WriteAsync());
        await otherDatabase.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(300);
        Assert.False(sameDatabase.IsCompleted, "the write on the same database did not wait for the checkpoint's gate");

        release.Set();
        Assert.Equal(MaintenanceStatus.Completed, (await checkpoint).Status);
        await sameDatabase.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(3L, await db.LongAsync("SELECT count(*) FROM items"));
    }

    [Theory(DisplayName = "AC-137-06 inside an open unit of work on the same database a gate-taking mode is refused with InvalidOperationException before SQL; Passive is allowed")]
    [InlineData(WalCheckpointMode.Full)]
    [InlineData(WalCheckpointMode.Restart)]
    [InlineData(WalCheckpointMode.Truncate)]
    public async Task The_holding_flow_is_refused(WalCheckpointMode mode)
    {
        await using var db = await WalFixture.WithItemsAsync(2);
        await using var unit = await UnitOfWork.BeginAsync(db.Context);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.Maintenance.CheckpointWalAsync(WalFixture.Request(mode, 1000)).WaitAsync(TimeSpan.FromSeconds(10)));
        var passive = await db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Passive, 1000));

        Assert.Contains("UnitOfWork", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("maint.db", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(db.Directory, refusal.Message, StringComparison.Ordinal);
        Assert.NotEqual(MaintenanceStatus.Busy, passive.Status);
        Assert.Equal([nameof(MaintenanceOperation.WalCheckpoint)], db.Sink.Operations);
        await unit.CommitAsync();
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.CheckpointWalAsync(WalFixture.Request(mode, 5000))).Status);
    }

    [Fact(DisplayName = "AC-137-06 a unit of work in another flow, or on another database, does not trigger the refusal")]
    public async Task Other_flows_and_databases_are_not_refused()
    {
        await using var db = await WalFixture.WithItemsAsync(2);
        await using var other = await WalFixture.WithItemsAsync(0);
        await using (var unit = await UnitOfWork.BeginAsync(other.Context))
        {
            var result = await db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000));
            Assert.Equal(MaintenanceStatus.Completed, result.Status);
            await unit.CommitAsync();
        }

        var holder = await GateHolder.HoldAsync(db.Context);
        var waiting = db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 10000));
        await Task.Delay(200);
        await holder.CommitAsync();
        Assert.Equal(MaintenanceStatus.Completed, (await waiting).Status);
    }

    [Fact(DisplayName = "AC-137-06 a committed or disposed unit of work no longer marks the flow")]
    public async Task A_closed_unit_of_work_releases_the_mark()
    {
        await using var db = await WalFixture.WithItemsAsync(2);
        var committed = await UnitOfWork.BeginAsync(db.Context);
        await committed.CommitAsync();
        await committed.DisposeAsync();
        var disposed = await UnitOfWork.BeginAsync(db.Context);
        await disposed.DisposeAsync();

        var result = await db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
    }
}

// AC-137-07: cancellation.
public sealed class WalCheckpointCancellationTests
{
    [Fact(DisplayName = "AC-137-07 a pre-cancelled token throws OperationCanceledException and runs no SQL")]
    public async Task A_cancelled_token_runs_nothing()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        var (context, source) = db.Counting();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate), cancelled.Token));

        Assert.Equal(0, source.Opens);
        Assert.Empty(db.Sink.Maintenance);
    }

    [Fact(DisplayName = "AC-137-07 cancelling during the gate wait releases the gate and the connection; the next write and checkpoint succeed")]
    public async Task Cancelling_the_gate_wait_releases_everything()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        var holder = await GateHolder.HoldAsync(db.Context);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 30000), cancel.Token));
        await holder.CommitAsync();

        await db.WriteAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var result = await db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000));
        Assert.Equal(MaintenanceStatus.Completed, result.Status);
    }

    [Fact(DisplayName = "AC-137-07 cancelling while the statement runs releases the gate; the next write succeeds")]
    public async Task Cancelling_the_statement_releases_the_gate()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        using var hook = new StatementStartHook();
        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            StatementStartHook.OnStart(cancel.Cancel);
            await db.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 30000), cancel.Token);
        });

        await db.WriteAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
}

// AC-137-08: the capability, appended, and the PostgreSQL refusal.
public sealed class WalCheckpointCapabilityTests
{
    [Fact(DisplayName = "AC-137-08 WalCheckpoint is appended after IndexExpression, so every earlier ordinal is unchanged")]
    public void The_capability_is_appended()
    {
        Assert.Equal(19, (int)Capability.IndexExpression);
        Assert.Equal(20, (int)Capability.WalCheckpoint);
        Assert.Equal(Support.Native, SqliteProvider.Instance.Capabilities.Of(Capability.WalCheckpoint));
        Assert.Equal(Support.None, PostgresProvider.Instance.Capabilities.Of(Capability.WalCheckpoint));
    }

    [Fact(DisplayName = "AC-137-08 both manifests carry the WalCheckpoint line")]
    public void Both_manifests_carry_the_line()
    {
        var root = RuntimeSources.LibraryRoot();
        Assert.Contains("WalCheckpoint=Native", File.ReadAllLines(Path.Combine(root, "src", "Sqlite", "assets", "ormlite.capabilities.sqlite.txt")));
        Assert.Contains("WalCheckpoint=None", File.ReadAllLines(Path.Combine(root, "src", "Postgres", "assets", "ormlite.capabilities.postgres.txt")));
    }

    [Theory(DisplayName = "AC-137-08 PostgreSQL refuses every mode with CapabilityNotSupportedException before any connection opens")]
    [InlineData(WalCheckpointMode.Passive)]
    [InlineData(WalCheckpointMode.Truncate)]
    public async Task Postgres_is_refused(WalCheckpointMode mode)
    {
        var context = new OrmContext(new NeverUsedDataSource(), PostgresProvider.Instance);

        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(mode)));

        Assert.Equal("postgres", refusal.Provider);
        Assert.Equal(nameof(OrmMaintenance.CheckpointWalAsync), refusal.Node);
        Assert.Equal(Capability.WalCheckpoint, refusal.Capability);
    }

    [Fact(DisplayName = "AC-137-08 a provider without the maintenance port is refused before any connection opens")]
    public async Task A_portless_provider_is_refused()
    {
        var context = new OrmContext(new NeverUsedDataSource(), new PortlessProvider());

        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Passive)));

        Assert.Equal(Capability.WalCheckpoint, refusal.Capability);
    }
}

// AC-137-09: one diagnostics event, no connection string; the docs.
public sealed class WalCheckpointDiagnosticsTests
{
    [Fact(DisplayName = "AC-137-09 one Maintenance event per call, tagged WalCheckpoint, carrying no connection string or file path")]
    public async Task One_event_without_secrets()
    {
        await using var db = await WalFixture.WithItemsAsync(2);
        db.Sink.Clear();

        await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Truncate));

        var statement = Assert.Single(db.Sink.Maintenance);
        Assert.Equal(StatementKind.Maintenance, statement.Kind);
        Assert.Equal(nameof(MaintenanceOperation.WalCheckpoint), statement.Operation);
        Assert.Equal("sqlite", statement.Provider);
        Assert.DoesNotContain("Data Source", statement.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(db.Path, statement.Text, StringComparison.Ordinal);
        Assert.Null(statement.Error);
    }

    [Fact(DisplayName = "AC-137-09 the activity of a maintenance statement carries the operation tag")]
    public async Task The_activity_is_tagged()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        var tags = new List<string?>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Neemle.OrmLite",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a => { if (a.GetTagItem("db.statement") is string text && text.Contains("wal_checkpoint", StringComparison.Ordinal)) lock (tags) tags.Add(a.GetTagItem("orm.operation") as string); },
        };
        ActivitySource.AddActivityListener(listener);

        await db.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Passive));

        lock (tags) Assert.Contains(nameof(MaintenanceOperation.WalCheckpoint), tags);
    }
}

// AC-137-09: the documentation.
public sealed class WalCheckpointDocsTests
{
    [Fact(DisplayName = "AC-137-09 docs/maintenance.md describes the modes, the statuses, readers and writers, timeout and cancellation, the PostgreSQL refusal and a runnable example, and docs/README.md links it")]
    public void The_page()
    {
        var section = MaintenanceDocs.Section("WAL checkpoint");

        foreach (var phrase in new[] { "`Passive`", "`Full`", "`Restart`", "`Truncate`", "`Completed`", "`Partial`", "`Busy`", "`NotWal`", "reader", "OperationCanceledException", "PostgreSQL is refused", "CheckpointWalAsync(" })
            Assert.Contains(phrase, section, StringComparison.Ordinal);
        Assert.Contains("writer gate", MaintenanceDocs.Page, StringComparison.Ordinal);
        Assert.Contains("Ops lane", MaintenanceDocs.Page, StringComparison.Ordinal);
        Assert.Contains("(maintenance.md)", File.ReadAllText(Path.Combine(RuntimeSources.LibraryRoot(), "docs", "README.md")), StringComparison.Ordinal);
    }
}
