using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

internal static class VacuumFixture
{
    internal const int Rows = 400;

    // N rows, then every second one deleted, so the file has free pages for VACUUM to reclaim.
    internal static async Task SeedWithFreePagesAsync(SqliteMaintenanceDatabase db)
    {
        await db.ExecAsync(WalFixture.Items);
        await using var connection = await db.RawAsync();
        await using (var insert = connection.BeginTransaction())
        {
            for (var i = 0; i < Rows; i++)
                await SqliteMaintenanceDatabase.Exec(connection, "INSERT INTO items (v) VALUES ('" + i + new string('p', 500) + "')", insert);
            await insert.CommitAsync();
        }
        await SqliteMaintenanceDatabase.Exec(connection, "DELETE FROM items WHERE id % 2 = 0");
    }

    internal static VacuumRequest Plain(int? timeoutMs = null) =>
        new(VacuumMode.Plain) { Timeout = timeoutMs is { } ms ? TimeSpan.FromMilliseconds(ms) : null };
}

// AC-134-01: a WAL database is checkpointed (Truncate), then vacuumed.
public sealed class VacuumSqliteOrderTests
{
    [Fact(DisplayName = "AC-134-01 Plain on a WAL file database checkpoints with Truncate, then vacuums: Completed, two events in order, no free pages, every remaining row intact")]
    public async Task Checkpoint_then_vacuum()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await VacuumFixture.SeedWithFreePagesAsync(db);
        var rows = await db.DumpAsync("SELECT id, v FROM items ORDER BY id");
        Assert.True(await db.LongAsync("PRAGMA freelist_count") > 0);
        db.Sink.Clear();

        var result = await db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.True(result.IsComplete);
        Assert.Equal(MaintenanceOperation.Vacuum, result.Operation);
        Assert.Equal(VacuumMode.Plain, result.Mode);
        Assert.Null(result.Table);
        Assert.NotNull(result.Checkpoint);
        Assert.Equal(WalCheckpointMode.Truncate, result.Checkpoint!.Mode);
        Assert.Equal(MaintenanceStatus.Completed, result.Checkpoint.Status);
        Assert.Equal([nameof(MaintenanceOperation.WalCheckpoint), nameof(MaintenanceOperation.Vacuum)], db.Sink.Operations);
        Assert.Equal("PRAGMA wal_checkpoint(TRUNCATE)", db.Sink.Maintenance[0].Text);
        Assert.Equal("VACUUM", db.Sink.Maintenance[1].Text);
        Assert.Equal(0L, await db.LongAsync("PRAGMA freelist_count"));
        Assert.Equal(VacuumFixture.Rows / 2, rows.Count);
        Assert.Equal(rows, await db.DumpAsync("SELECT id, v FROM items ORDER BY id"));
    }

    [Fact(DisplayName = "AC-134-01 the prerequisite is Completed or NotWal, never Partial or Busy")]
    public void The_prerequisite_predicate()
    {
        WalCheckpointResult Of(MaintenanceStatus status) => new("sqlite", WalCheckpointMode.Truncate, status, 1, 1);

        Assert.True(OrmMaintenance.CheckpointLetsVacuumRun(Of(MaintenanceStatus.Completed)));
        Assert.True(OrmMaintenance.CheckpointLetsVacuumRun(Of(MaintenanceStatus.NotWal)));
        Assert.False(OrmMaintenance.CheckpointLetsVacuumRun(Of(MaintenanceStatus.Partial)));
        Assert.False(OrmMaintenance.CheckpointLetsVacuumRun(Of(MaintenanceStatus.Busy)));
    }
}

// AC-134-02: a database that is not in WAL mode is vacuumed after a NotWal checkpoint.
public sealed class VacuumSqliteNotWalTests
{
    [Fact(DisplayName = "AC-134-02 a rollback-journal file database: Completed, checkpoint NotWal, one VACUUM event after it, no free pages")]
    public async Task A_rollback_journal_is_vacuumed()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        await VacuumFixture.SeedWithFreePagesAsync(db);
        Assert.True(await db.LongAsync("PRAGMA freelist_count") > 0);
        db.Sink.Clear();

        var result = await db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.Equal(MaintenanceStatus.NotWal, result.Checkpoint?.Status);
        Assert.Equal([nameof(MaintenanceOperation.WalCheckpoint), nameof(MaintenanceOperation.Vacuum)], db.Sink.Operations);
        Assert.Equal(0L, await db.LongAsync("PRAGMA freelist_count"));
    }

    [Fact(DisplayName = "AC-134-02 an in-memory database: Completed, checkpoint NotWal, then one VACUUM event")]
    public async Task An_in_memory_database_is_vacuumed()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenMemoryAsync();
        db.Sink.Clear();

        var result = await db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.Equal(MaintenanceStatus.NotWal, result.Checkpoint?.Status);
        Assert.Equal([nameof(MaintenanceOperation.WalCheckpoint), nameof(MaintenanceOperation.Vacuum)], db.Sink.Operations);
    }
}

// AC-134-03: the checkpoint prerequisite is not met while a reader holds the log.
public sealed class VacuumSqlitePrerequisiteTests
{
    [Fact(DisplayName = "AC-134-03 with an open reader VACUUM is NotRun within the timeout plus 2 s, issues no VACUUM, changes no page and leaves the reader's snapshot; ThrowIfNotComplete throws with the same result; after the reader ends it completes")]
    public async Task A_reader_prevents_vacuum()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await VacuumFixture.SeedWithFreePagesAsync(db);
        await using var reader = await db.RawAsync();
        await using var snapshot = reader.BeginTransaction(deferred: true);
        var seen = await CountAsync(reader, snapshot);
        for (var i = 0; i < 5; i++) await db.ExecAsync("INSERT INTO items (v) VALUES ('newer')");
        var pages = await db.LongAsync("PRAGMA page_count");
        var free = await db.LongAsync("PRAGMA freelist_count");
        db.Sink.Clear();

        var clock = Stopwatch.StartNew();
        var result = await db.Maintenance.VacuumAsync(VacuumFixture.Plain(200));
        clock.Stop();

        Assert.Equal(MaintenanceStatus.NotRun, result.Status);
        Assert.Contains(result.Checkpoint?.Status, new MaintenanceStatus?[] { MaintenanceStatus.Busy, MaintenanceStatus.Partial });
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(200) + SqliteMaintenanceDatabase.Margin, "took " + clock.Elapsed);
        Assert.DoesNotContain(nameof(MaintenanceOperation.Vacuum), db.Sink.Operations);
        Assert.Equal(pages, await db.LongAsync("PRAGMA page_count"));
        Assert.Equal(free, await db.LongAsync("PRAGMA freelist_count"));
        Assert.Equal(seen, await CountAsync(reader, snapshot));
        var thrown = Assert.Throws<MaintenanceIncompleteException>(result.ThrowIfNotComplete);
        Assert.Same(result, thrown.Result);

        await snapshot.CommitAsync();
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.VacuumAsync(VacuumFixture.Plain(5000))).Status);
    }

    [Fact(DisplayName = "AC-134-03 ThrowIfNotComplete returns quietly for a complete result")]
    public void A_complete_result_does_not_throw()
    {
        new VacuumResult("sqlite", VacuumMode.Plain, null, MaintenanceStatus.Completed, null).ThrowIfNotComplete();
        Assert.Throws<ArgumentNullException>(() => MaintenanceResultExtensions.ThrowIfNotComplete(null!));
    }

    private static async Task<long> CountAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT count(*) FROM items";
        return (long)(await command.ExecuteScalarAsync())!;
    }
}

// AC-134-04 and AC-134-06: gate contention and the interplay with writers.
public sealed class VacuumSqliteContentionTests
{
    [Fact(DisplayName = "AC-134-04 when another flow's unit of work holds the gate VACUUM is Busy within the timeout plus 2 s with no checkpoint and no VACUUM; after the holder commits it completes and the holder's write is intact")]
    public async Task A_busy_gate_is_busy()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(WalFixture.Items);
        var holder = await GateHolder.HoldAsync(db.Context, "INSERT INTO items (v) VALUES ('held')");
        db.Sink.Clear();

        var clock = Stopwatch.StartNew();
        var result = await db.Maintenance.VacuumAsync(VacuumFixture.Plain(200));
        clock.Stop();

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Null(result.Checkpoint);
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(200) + SqliteMaintenanceDatabase.Margin);
        Assert.DoesNotContain(nameof(MaintenanceOperation.Vacuum), db.Sink.Operations);

        await holder.CommitAsync();
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.VacuumAsync(VacuumFixture.Plain(5000))).Status);
        Assert.Equal(1L, await db.LongAsync("SELECT count(*) FROM items WHERE v = 'held'"));
    }

    [Fact(DisplayName = "AC-134-04 when the gate is busy only for the VACUUM step the result is Busy and carries the completed checkpoint")]
    public async Task A_gate_busy_after_the_checkpoint_keeps_the_checkpoint()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(WalFixture.Items);
        using var hook = new StatementStartHook();
        GateHolder? holder = null;
        Task<GateHolder>? holding = null;

        var result = await Run();

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Equal(MaintenanceStatus.Completed, result.Checkpoint?.Status);
        holder = await holding!;
        await holder.CommitAsync();

        async Task<VacuumResult> Run()
        {
            // While the checkpoint holds the gate, another flow queues for it, so it takes the gate before VACUUM does.
            StatementStartHook.OnStart(() =>
            {
                StatementStartHook.OnStart(null);
                holding = Task.Run(() => GateHolder.HoldAsync(db.Context));
                Thread.Sleep(200);
            });
            return await db.Maintenance.VacuumAsync(VacuumFixture.Plain(1500));
        }
    }

    [Fact(DisplayName = "AC-134-04 SQLITE_BUSY from another connection's lock is reported as Busy, never thrown")]
    public async Task A_database_lock_is_busy()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        await using var blocker = await db.RawAsync();
        await using var exclusive = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(blocker, "INSERT INTO seed DEFAULT VALUES", exclusive);

        var clock = Stopwatch.StartNew();
        var result = await db.Maintenance.VacuumAsync(VacuumFixture.Plain(200));
        clock.Stop();

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Equal(MaintenanceStatus.NotWal, result.Checkpoint?.Status);
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(200) + SqliteMaintenanceDatabase.Margin, "took " + clock.Elapsed);
        await exclusive.CommitAsync();
    }

    [Fact(DisplayName = "AC-134-06 while VACUUM holds the gate an ORM write on the same database waits and then completes; a write on another file is not delayed")]
    public async Task Writes_wait_for_vacuum_on_the_same_database_only()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(WalFixture.Items);
        await using var other = await SqliteMaintenanceDatabase.OpenWalAsync();
        await other.ExecAsync(WalFixture.Items);
        using var hook = new StatementStartHook();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var vacuum = Task.Run(async () =>
        {
            var statements = 0;
            StatementStartHook.OnStart(() =>
            {
                if (Interlocked.Increment(ref statements) != 2) return;
                started.Set();
                release.Wait(TimeSpan.FromSeconds(20));
            });
            return await db.Maintenance.VacuumAsync(VacuumFixture.Plain(20000));
        });
        Assert.True(started.Wait(TimeSpan.FromSeconds(10)));

        var sameDatabase = Task.Run(() => db.WriteAsync());
        await Task.Run(() => other.WriteAsync()).WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(300);
        Assert.False(sameDatabase.IsCompleted, "the write did not wait for VACUUM's gate");

        release.Set();
        Assert.Equal(MaintenanceStatus.Completed, (await vacuum).Status);
        await sameDatabase.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1L, await db.LongAsync("SELECT count(*) FROM items"));
    }
}

// AC-134-05: a checkpoint failure propagates and releases everything.
public sealed class VacuumSqliteFailureTests
{
    [Fact(DisplayName = "AC-134-05 a checkpoint that fails with SQLITE_NOTADB propagates the same SqliteException, issues no VACUUM, and releases the gate and connection")]
    public async Task A_failed_checkpoint_propagates()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        SqliteConnection.ClearAllPools();
        var sound = await File.ReadAllBytesAsync(db.Path);
        await File.WriteAllTextAsync(db.Path, new string('x', 8192));
        db.Sink.Clear();

        var failure = await Assert.ThrowsAsync<SqliteException>(() => db.Maintenance.VacuumAsync(VacuumFixture.Plain(2000)));

        Assert.Equal(26, failure.SqliteErrorCode);
        Assert.DoesNotContain(nameof(MaintenanceOperation.Vacuum), db.Sink.Operations);
        SqliteConnection.ClearAllPools();
        await File.WriteAllBytesAsync(db.Path, sound);
        var result = await db.Maintenance.VacuumAsync(VacuumFixture.Plain(2000));
        Assert.Equal(MaintenanceStatus.Completed, result.Status);
    }

    [Fact(DisplayName = "AC-134-05 a VACUUM statement that fails propagates unwrapped and releases the gate")]
    public async Task A_failed_vacuum_propagates()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(WalFixture.Items);
        var provider = new TunedSqliteProvider(SqliteProvider.Instance.Capabilities) { VacuumText = "VACUUM no_such_schema" };
        var (context, _) = db.Counting(provider);

        var failure = await Assert.ThrowsAsync<SqliteException>(() => context.Maintenance.VacuumAsync(VacuumFixture.Plain(2000)));

        Assert.Equal(1, failure.SqliteErrorCode);
        await db.WriteAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
}

// AC-134-09 to AC-134-13: refusals and validation, all before any connection opens.
public sealed class VacuumRefusalTests
{
    private static readonly EntityDescriptor Items = Descriptors.Table("items");

    [Fact(DisplayName = "AC-134-09 FULL on SQLite is refused with VacuumFull, operation VacuumAsync, before any connection")]
    public async Task Full_is_refused_on_sqlite()
    {
        var context = new OrmContext(new NeverUsedDataSource(), SqliteProvider.Instance);

        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Full)));

        Assert.Equal(Capability.VacuumFull, refusal.Capability);
        Assert.Equal(nameof(OrmMaintenance.VacuumAsync), refusal.Node);
        Assert.Equal("sqlite", refusal.Provider);
    }

    [Fact(DisplayName = "AC-134-10 a Table on SQLite is refused with VacuumTable before any connection")]
    public async Task A_table_is_refused_on_sqlite()
    {
        var context = new OrmContext(new NeverUsedDataSource(), SqliteProvider.Instance);

        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain) { Table = Items }));

        Assert.Equal(Capability.VacuumTable, refusal.Capability);
    }

    [Fact(DisplayName = "AC-134-09 the operation's capability is checked before the option's")]
    public async Task The_operation_capability_comes_first()
    {
        var context = new OrmContext(new NeverUsedDataSource(), TunedSqliteProvider.Without(Capability.Vacuum));

        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Full) { Table = Items }));

        Assert.Equal(Capability.Vacuum, refusal.Capability);
    }

    [Fact(DisplayName = "AC-134-11 inside an open unit of work on the same database VACUUM is refused naming the UnitOfWork, with no SQL; after the commit it succeeds")]
    public async Task The_holding_flow_is_refused()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(WalFixture.Items);
        var unit = await UnitOfWork.BeginAsync(db.Context);
        db.Sink.Clear();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => db.Maintenance.VacuumAsync(VacuumFixture.Plain(1000)).WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Contains("UnitOfWork", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(db.Sink.Maintenance);
        await unit.CommitAsync();
        await unit.DisposeAsync();
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.VacuumAsync(VacuumFixture.Plain(5000))).Status);
    }

    [Fact(DisplayName = "AC-134-11 a unit of work on another database does not trigger the refusal")]
    public async Task Another_database_is_not_refused()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await using var other = await SqliteMaintenanceDatabase.OpenWalAsync();
        await using var unit = await UnitOfWork.BeginAsync(other.Context);

        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.VacuumAsync(VacuumFixture.Plain(5000))).Status);
    }

    [Fact(DisplayName = "AC-134-12 a provider without the maintenance port is refused with Vacuum before any connection")]
    public async Task A_portless_provider_is_refused()
    {
        var context = new OrmContext(new NeverUsedDataSource(), new PortlessProvider());

        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain)));

        Assert.Equal(Capability.Vacuum, refusal.Capability);
        Assert.Equal("portless", refusal.Provider);
    }

    [Fact(DisplayName = "AC-134-13 a null request, a zero or undefined mode and a non-positive timeout are refused before the capability check")]
    public async Task Validation_precedes_the_capability_check()
    {
        var context = new OrmContext(new NeverUsedDataSource(), new PortlessProvider());

        await Assert.ThrowsAsync<ArgumentNullException>(() => context.Maintenance.VacuumAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.Maintenance.VacuumAsync(new VacuumRequest((VacuumMode)0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.Maintenance.VacuumAsync(new VacuumRequest((VacuumMode)99)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain) { Timeout = TimeSpan.Zero }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain) { Timeout = TimeSpan.FromMilliseconds(-5) }));
        Assert.False(Enum.IsDefined((VacuumMode)0));
        Assert.Equal([1, 2], Enum.GetValues<VacuumMode>().Select(m => (int)m));
    }

    [Fact(DisplayName = "AC-134-09 SQLite renders only a plain whole-database VACUUM")]
    public void Sqlite_renders_only_plain()
    {
        Assert.Equal("VACUUM", SqliteProvider.Instance.RenderVacuum(VacuumMode.Plain, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => SqliteProvider.Instance.RenderVacuum(VacuumMode.Full, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => SqliteProvider.Instance.RenderVacuum(VacuumMode.Plain, Items));
    }
}

// AC-134-14: cancellation.
public sealed class VacuumCancellationTests
{
    [Fact(DisplayName = "AC-134-14 a pre-cancelled token throws with no connection opened and no event")]
    public async Task A_cancelled_token_runs_nothing()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        var (context, source) = db.Counting();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        db.Sink.Clear();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Maintenance.VacuumAsync(VacuumFixture.Plain(), cancelled.Token));

        Assert.Equal(0, source.Opens);
        Assert.Empty(db.Sink.Maintenance);
    }

    [Fact(DisplayName = "AC-134-14 cancelling while the VACUUM step waits on the gate after a completed checkpoint releases everything; the next ORM write proceeds at once")]
    public async Task Cancelling_the_vacuum_gate_wait_releases_everything()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(WalFixture.Items);
        using var hook = new StatementStartHook();
        using var cancel = new CancellationTokenSource();
        Task<GateHolder>? holding = null;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            StatementStartHook.OnStart(() =>
            {
                StatementStartHook.OnStart(null);
                holding = Task.Run(() => GateHolder.HoldAsync(db.Context));
                Thread.Sleep(200);
                cancel.CancelAfter(500);
            });
            await db.Maintenance.VacuumAsync(VacuumFixture.Plain(30000), cancel.Token);
        });

        Assert.Equal([nameof(MaintenanceOperation.WalCheckpoint)], db.Sink.Operations);
        await (await holding!).CommitAsync();
        await db.WriteAsync().WaitAsync(TimeSpan.FromSeconds(2));
    }
}

// AC-134-15: the appended members.
public sealed class VacuumCapabilityTests
{
    [Fact(DisplayName = "AC-134-15 Vacuum, VacuumFull and VacuumTable follow WalCheckpoint; Vacuum and NotRun are appended to their enums")]
    public void The_members_are_appended()
    {
        Assert.Equal(20, (int)Capability.WalCheckpoint);
        Assert.Equal(21, (int)Capability.Vacuum);
        Assert.Equal(22, (int)Capability.VacuumFull);
        Assert.Equal(23, (int)Capability.VacuumTable);
        Assert.Equal(2, (int)MaintenanceOperation.Vacuum);
        Assert.Equal(1, (int)MaintenanceStatus.Completed);
        Assert.Equal(4, (int)MaintenanceStatus.NotWal);
        Assert.Equal(5, (int)MaintenanceStatus.NotRun);
    }

    [Fact(DisplayName = "AC-134-15 SQLite declares Vacuum Native, VacuumFull and VacuumTable None; PostgreSQL Native for all three")]
    public void The_levels_are_declared()
    {
        var sqlite = SqliteProvider.Instance.Capabilities;
        var postgres = Postgres.PostgresProvider.Instance.Capabilities;

        Assert.Equal(Support.Native, sqlite.Of(Capability.Vacuum));
        Assert.Equal(Support.None, sqlite.Of(Capability.VacuumFull));
        Assert.Equal(Support.None, sqlite.Of(Capability.VacuumTable));
        Assert.Equal(Support.Native, postgres.Of(Capability.Vacuum));
        Assert.Equal(Support.Native, postgres.Of(Capability.VacuumFull));
        Assert.Equal(Support.Native, postgres.Of(Capability.VacuumTable));
    }
}

// The shared contention classifier and session guard, which 134 ships for every later operation.
public sealed class MaintenanceContentionMappingTests
{
    [Theory(DisplayName = "SQLite reports SQLITE_BUSY and SQLITE_LOCKED, extended codes included, as contention and nothing else")]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(261, true)]
    [InlineData(517, true)]
    [InlineData(262, true)]
    [InlineData(1, false)]
    [InlineData(11, false)]
    [InlineData(26, false)]
    public void Sqlite_contention(int code, bool contention) =>
        Assert.Equal(contention, SqliteProvider.Instance.IsContention(new SqliteException("probe", code)));

    [Fact(DisplayName = "SQLite has a writer gate and renders no session guard; a provider that keeps the defaults has neither and reports no contention")]
    public void Defaults()
    {
        Assert.True(SqliteProvider.Instance.HasWriterGate);
        Assert.Null(((IMaintenanceProvider)SqliteProvider.Instance).RenderLockGuard(TimeSpan.FromSeconds(3)));
        IMaintenanceProvider bare = new BareMaintenanceProvider();
        Assert.False(bare.HasWriterGate);
        Assert.False(bare.IsContention(new SqliteException("probe", 5)));
        Assert.Null(bare.RenderLockGuard(TimeSpan.FromSeconds(3)));
        Assert.Throws<NotSupportedException>(() => bare.RenderVacuum(VacuumMode.Plain, null));
        Assert.Throws<NotSupportedException>(() => bare.RenderWalCheckpoint(WalCheckpointMode.Passive));
    }

    [Fact(DisplayName = "the pipeline runs the session guard around the statement on the same connection, without a diagnostics event, and tears it down when the statement fails")]
    public async Task The_guard_wraps_the_statement()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync("CREATE TABLE guard_log (n INTEGER PRIMARY KEY, step TEXT NOT NULL)");
        var guard = new MaintenanceSessionGuard("INSERT INTO guard_log (step) VALUES ('setup')", "INSERT INTO guard_log (step) VALUES ('teardown')");
        var (context, _) = db.Counting(new TunedSqliteProvider(SqliteProvider.Instance.Capabilities) { Guard = guard });
        db.Sink.Clear();

        await context.Maintenance.CheckpointWalAsync(new WalCheckpointRequest(WalCheckpointMode.Passive));

        Assert.Equal(["setup", "teardown"], await db.DumpAsync("SELECT step FROM guard_log ORDER BY n"));
        Assert.Equal([nameof(MaintenanceOperation.WalCheckpoint)], db.Sink.Operations);

        var failing = new TunedSqliteProvider(SqliteProvider.Instance.Capabilities) { Guard = guard, VacuumText = "VACUUM no_such_schema" };
        var (failingContext, _) = db.Counting(failing);
        await Assert.ThrowsAsync<SqliteException>(() => failingContext.Maintenance.VacuumAsync(VacuumFixture.Plain(2000)));
        Assert.Equal(["setup", "teardown", "setup", "teardown", "setup", "teardown"], await db.DumpAsync("SELECT step FROM guard_log ORDER BY n"));
    }

    [Fact(DisplayName = "while a session guard is set the statement's command timeout gets one second of grace over the budget, so a server-side lock timeout fires first")]
    public async Task A_guarded_statement_gets_a_second_of_grace()
    {
        // SQLite retries a busy statement until its command timeout, so the time to Busy shows the timeout the pipeline set.
        await using var db = await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        await using var blocker = await db.RawAsync();
        await using var immediate = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(blocker, "INSERT INTO seed DEFAULT VALUES", immediate);
        var noop = new MaintenanceSessionGuard("SELECT 1", "SELECT 1");
        var (guarded, _) = db.Counting(new TunedSqliteProvider(SqliteProvider.Instance.Capabilities) { Guard = noop });
        var (plain, _) = db.Counting(new TunedSqliteProvider(SqliteProvider.Instance.Capabilities));

        var withGuard = await TimeToBusyAsync(guarded);
        var withoutGuard = await TimeToBusyAsync(plain);

        Assert.True(withGuard >= TimeSpan.FromMilliseconds(1700), "guarded statement gave up after " + withGuard);
        Assert.True(withoutGuard < TimeSpan.FromMilliseconds(1700), "unguarded statement gave up after " + withoutGuard);
        await immediate.CommitAsync();

        static async Task<TimeSpan> TimeToBusyAsync(OrmContext context)
        {
            var clock = Stopwatch.StartNew();
            var result = await context.Maintenance.VacuumAsync(VacuumFixture.Plain(200));
            clock.Stop();
            Assert.Equal(MaintenanceStatus.Busy, result.Status);
            return clock.Elapsed;
        }
    }

    private sealed class BareMaintenanceProvider : IMaintenanceProvider
    {
        public TimeSpan MaintenanceTimeout => TimeSpan.FromSeconds(1);

        public string? WriterKeyOf(System.Data.Common.DbDataSource source) => null;

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(System.Data.Common.DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
            ValueTask.FromResult(new MaintenanceWriteLease(true, null));
    }
}

// AC-134-16: the documentation.
public sealed class VacuumDocsTests
{
    [Fact(DisplayName = "AC-134-16 docs/maintenance.md has a VACUUM section with the scope, the SQLite order, NotRun, the gate release, FULL's rewrite and lock, the budget and ThrowIfNotComplete")]
    public void The_section()
    {
        var section = MaintenanceDocs.Section("VACUUM");

        foreach (var phrase in new[] { "`Plain`", "`Full`", "one table", "`Truncate`", "`NotRun`", "releases it", "rewrites the table", "operating system", "ACCESS EXCLUSIVE", "never turned", "does not checkpoint again" })
            Assert.Contains(phrase, section, StringComparison.Ordinal);
        foreach (var phrase in new[] { "ONE budget", "lock_timeout", "55P03", "SQLITE_BUSY", "ThrowIfNotComplete", "MaintenanceIncompleteException" })
            Assert.Contains(phrase, MaintenanceDocs.Page, StringComparison.Ordinal);
    }
}
