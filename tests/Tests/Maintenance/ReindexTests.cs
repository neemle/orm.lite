using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

internal static class ReindexFixture
{
    // Table A: a unique index and a live-only filtered index; table B: one index. Descriptors name what the schema has.
    internal static readonly EntityDescriptor A = EntityDescriptor.Of("FixtureA", "a", true)
        .Index("ix_a_u", true, false, "u")
        .Index("ix_a_k_live", false, true, "k");

    internal static readonly EntityDescriptor B = EntityDescriptor.Of("FixtureB", "b", true).Index("ix_b_k", false, false, "k");

    internal static IndexDescriptor Unique => A.Indexes[0];

    internal static IndexDescriptor Live => A.Indexes[1];

    internal static readonly string[] Schema =
    [
        "CREATE TABLE a (id INTEGER PRIMARY KEY, u TEXT NOT NULL, k TEXT NOT NULL, deleted_at TEXT NULL)",
        "CREATE UNIQUE INDEX ix_a_u ON a (u)",
        "CREATE INDEX ix_a_k_live ON a (k) WHERE deleted_at IS NULL",
        "CREATE TABLE b (id INTEGER PRIMARY KEY, k TEXT NOT NULL)",
        "CREATE INDEX ix_b_k ON b (k)",
    ];

    internal static async Task<SqliteMaintenanceDatabase> OpenAsync()
    {
        var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(WalFixture.Items);
        foreach (var statement in Schema) await db.ExecAsync(statement);
        for (var i = 1; i <= 40; i++)
        {
            await db.ExecAsync("INSERT INTO a (u, k, deleted_at) VALUES ('u" + i + "', 'k" + (i % 5) + "', " + (i % 4 == 0 ? "'2026-01-01'" : "NULL") + ")");
            await db.ExecAsync("INSERT INTO b (k) VALUES ('k" + (i % 3) + "')");
        }
        return db;
    }

    internal static Task<List<string>> DumpAsync(SqliteMaintenanceDatabase db) =>
        db.DumpAsync("SELECT 'a', id, u, k, deleted_at FROM a UNION ALL SELECT 'b', id, k, NULL, NULL FROM b ORDER BY 1, 2");

    internal static ReindexRequest Request(ReindexScope scope, ReindexMode mode = ReindexMode.Blocking, int? timeoutMs = null) => scope switch
    {
        ReindexScope.Database => new(scope, mode) { Timeout = Ms(timeoutMs) },
        ReindexScope.Table => new(scope, mode) { Table = A, Timeout = Ms(timeoutMs) },
        _ => new(scope, mode) { Table = A, Index = Unique, Timeout = Ms(timeoutMs) },
    };

    private static TimeSpan? Ms(int? milliseconds) => milliseconds is { } ms ? TimeSpan.FromMilliseconds(ms) : null;
}

// AC-136-01: explicit scope and mode, rendered from the enums and the descriptors only.
public sealed class ReindexRenderTests
{
    [Fact(DisplayName = "AC-136-01 SQLite renders REINDEX, REINDEX main.<table> and REINDEX main.<index>")]
    public void Sqlite_forms()
    {
        Assert.Equal("REINDEX", SqliteProvider.Instance.RenderReindex(ReindexScope.Database, ReindexMode.Blocking, null, null));
        Assert.Equal("REINDEX main.\"a\"", SqliteProvider.Instance.RenderReindex(ReindexScope.Table, ReindexMode.Blocking, ReindexFixture.A, null));
        Assert.Equal("REINDEX main.\"ix_a_u\"", SqliteProvider.Instance.RenderReindex(ReindexScope.Index, ReindexMode.Blocking, ReindexFixture.A, ReindexFixture.Unique));
        Assert.Throws<ArgumentOutOfRangeException>(() => SqliteProvider.Instance.RenderReindex(ReindexScope.Table, ReindexMode.Concurrent, ReindexFixture.A, null));
    }

    [Theory(DisplayName = "AC-136-01 PostgreSQL renders REINDEX DATABASE|TABLE|INDEX, with CONCURRENTLY exactly in Concurrent mode")]
    [InlineData(ReindexScope.Database, ReindexMode.Blocking, "REINDEX DATABASE")]
    [InlineData(ReindexScope.Database, ReindexMode.Concurrent, "REINDEX DATABASE CONCURRENTLY")]
    [InlineData(ReindexScope.Table, ReindexMode.Blocking, "REINDEX TABLE \"a\"")]
    [InlineData(ReindexScope.Table, ReindexMode.Concurrent, "REINDEX TABLE CONCURRENTLY \"a\"")]
    [InlineData(ReindexScope.Index, ReindexMode.Blocking, "REINDEX INDEX \"ix_a_u\"")]
    [InlineData(ReindexScope.Index, ReindexMode.Concurrent, "REINDEX INDEX CONCURRENTLY \"ix_a_u\"")]
    public void Postgres_forms(ReindexScope scope, ReindexMode mode, string expected)
    {
        var request = ReindexFixture.Request(scope, mode);

        Assert.Equal(expected, PostgresProvider.Instance.RenderReindex(scope, mode, request.Table, request.Index));
    }

    [Fact(DisplayName = "AC-136-01 the scope and mode enums have no zero member")]
    public void The_enums_are_explicit()
    {
        Assert.Equal([1, 2, 3], Enum.GetValues<ReindexScope>().Select(s => (int)s));
        Assert.Equal([1, 2], Enum.GetValues<ReindexMode>().Select(m => (int)m));
        Assert.False(Enum.IsDefined((ReindexScope)0));
        Assert.False(Enum.IsDefined((ReindexMode)0));
    }
}

// AC-136-02: validation before anything else.
public sealed class ReindexValidationTests
{
    public static TheoryData<ReindexRequest?, Type> Invalid() => new()
    {
        { null, typeof(ArgumentNullException) },
        { new ReindexRequest((ReindexScope)0, ReindexMode.Blocking), typeof(ArgumentOutOfRangeException) },
        { new ReindexRequest(ReindexScope.Database, (ReindexMode)0), typeof(ArgumentOutOfRangeException) },
        { new ReindexRequest(ReindexScope.Database, (ReindexMode)5), typeof(ArgumentOutOfRangeException) },
        { new ReindexRequest(ReindexScope.Database, ReindexMode.Blocking) { Table = ReindexFixture.A }, typeof(ArgumentException) },
        { new ReindexRequest(ReindexScope.Database, ReindexMode.Blocking) { Index = ReindexFixture.Unique }, typeof(ArgumentException) },
        { new ReindexRequest(ReindexScope.Table, ReindexMode.Blocking), typeof(ArgumentException) },
        { new ReindexRequest(ReindexScope.Table, ReindexMode.Blocking) { Table = ReindexFixture.A, Index = ReindexFixture.Unique }, typeof(ArgumentException) },
        { new ReindexRequest(ReindexScope.Index, ReindexMode.Blocking) { Table = ReindexFixture.A }, typeof(ArgumentException) },
        { new ReindexRequest(ReindexScope.Index, ReindexMode.Blocking) { Index = ReindexFixture.Unique }, typeof(ArgumentException) },
        { new ReindexRequest(ReindexScope.Index, ReindexMode.Blocking) { Table = ReindexFixture.A, Index = ReindexFixture.B.Indexes[0] }, typeof(ArgumentException) },
        { new ReindexRequest(ReindexScope.Table, ReindexMode.Blocking) { Table = EntityDescriptor.Of("FixtureV", "v", true).View() }, typeof(ArgumentException) },
        { new ReindexRequest(ReindexScope.Database, ReindexMode.Blocking) { Timeout = TimeSpan.Zero }, typeof(ArgumentOutOfRangeException) },
    };

    [Theory(DisplayName = "AC-136-02 an invalid request throws before the capability check and before any connection")]
    [MemberData(nameof(Invalid))]
    public async Task Invalid_requests_are_refused(ReindexRequest? request, Type expected)
    {
        foreach (var provider in new ISqlProvider[] { SqliteProvider.Instance, new PortlessProvider() })
        {
            var context = new OrmContext(new NeverUsedDataSource(), provider);

            var thrown = await Assert.ThrowsAnyAsync<ArgumentException>(() => context.Maintenance.ReindexAsync(request!));

            Assert.IsType(expected, thrown);
        }
    }
}

// AC-136-03: table data preserved on SQLite for every scope.
public sealed class ReindexSqlitePreservationTests
{
    [Theory(DisplayName = "AC-136-03 a blocking REINDEX of every scope keeps the data, the unique index still refuses a duplicate, and integrity_check is ok")]
    [InlineData(ReindexScope.Database)]
    [InlineData(ReindexScope.Table)]
    [InlineData(ReindexScope.Index)]
    public async Task Data_is_preserved(ReindexScope scope)
    {
        await using var db = await ReindexFixture.OpenAsync();
        var before = await ReindexFixture.DumpAsync(db);
        db.Sink.Clear();

        var result = await db.Maintenance.ReindexAsync(ReindexFixture.Request(scope));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.Equal(before, await ReindexFixture.DumpAsync(db));
        var duplicate = await Assert.ThrowsAsync<SqliteException>(() => db.ExecAsync("INSERT INTO a (u, k) VALUES ('u1', 'x')"));
        Assert.Equal(19, duplicate.SqliteErrorCode);
        Assert.Equal(["ok"], await db.DumpAsync("PRAGMA integrity_check"));
        Assert.Equal(nameof(MaintenanceOperation.Reindex), Assert.Single(db.Sink.Maintenance).Operation);
    }
}

// AC-136-05 and AC-136-06: refusals.
public sealed class ReindexRefusalTests
{
    [Theory(DisplayName = "AC-136-05 Concurrent on SQLite is refused with ReindexConcurrently, never downgraded, before any connection or SQL")]
    [InlineData(ReindexScope.Database)]
    [InlineData(ReindexScope.Table)]
    [InlineData(ReindexScope.Index)]
    public async Task Concurrent_is_refused_on_sqlite(ReindexScope scope)
    {
        var context = new OrmContext(new NeverUsedDataSource(), SqliteProvider.Instance);

        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.ReindexAsync(ReindexFixture.Request(scope, ReindexMode.Concurrent)));

        Assert.Equal(Capability.ReindexConcurrently, refusal.Capability);
        Assert.Equal(nameof(OrmMaintenance.ReindexAsync), refusal.Node);
        Assert.Equal("sqlite", refusal.Provider);
    }

    [Fact(DisplayName = "AC-136-06 a provider without the port, or declaring Reindex None, is refused with Reindex before any connection, also in Concurrent mode")]
    public async Task Unsupported_providers_are_refused()
    {
        foreach (var provider in new ISqlProvider[] { new PortlessProvider(), TunedSqliteProvider.Without(Capability.Reindex) })
        {
            var context = new OrmContext(new NeverUsedDataSource(), provider);

            var blocking = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Database)));
            var concurrent = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Database, ReindexMode.Concurrent)));

            Assert.Equal(Capability.Reindex, blocking.Capability);
            Assert.Equal(Capability.Reindex, concurrent.Capability);
        }
    }

    [Fact(DisplayName = "AC-136-06 Reindex and ReindexConcurrently follow Analyze; SQLite Native and None, PostgreSQL Native and Native")]
    public void The_capabilities_are_appended()
    {
        Assert.Equal(24, (int)Capability.Analyze);
        Assert.Equal(25, (int)Capability.Reindex);
        Assert.Equal(26, (int)Capability.ReindexConcurrently);
        Assert.Equal(4, (int)MaintenanceOperation.Reindex);
        Assert.Equal(Support.Native, SqliteProvider.Instance.Capabilities.Of(Capability.Reindex));
        Assert.Equal(Support.None, SqliteProvider.Instance.Capabilities.Of(Capability.ReindexConcurrently));
        Assert.Equal(Support.Native, PostgresProvider.Instance.Capabilities.Of(Capability.Reindex));
        Assert.Equal(Support.Native, PostgresProvider.Instance.Capabilities.Of(Capability.ReindexConcurrently));
    }
}

// AC-136-07: the SQLite gate and the holder.
public sealed class ReindexSqliteGateTests
{
    [Fact(DisplayName = "AC-136-07 a gate held by another flow gives Busy with no connection opened")]
    public async Task A_busy_gate_opens_nothing()
    {
        await using var db = await ReindexFixture.OpenAsync();
        var (context, source) = db.Counting();
        await using var holder = await GateHolder.HoldAsync(db.Context);

        var result = await context.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Database, timeoutMs: 200));

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Equal(0, source.Opens);
    }

    [Fact(DisplayName = "AC-136-07 an ORM write on the same database issued during REINDEX waits and then completes")]
    public async Task A_write_waits_for_reindex()
    {
        await using var db = await ReindexFixture.OpenAsync();
        using var hook = new StatementStartHook();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var reindex = Task.Run(async () =>
        {
            StatementStartHook.OnStart(() => { started.Set(); release.Wait(TimeSpan.FromSeconds(20)); });
            return await db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Table, timeoutMs: 20000));
        });
        Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
        var write = Task.Run(() => db.WriteAsync());
        await Task.Delay(300);
        Assert.False(write.IsCompleted, "the write did not wait for REINDEX's gate");

        release.Set();
        Assert.Equal(MaintenanceStatus.Completed, (await reindex).Status);
        await write.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact(DisplayName = "AC-136-07 inside an open unit of work on the same database REINDEX is refused before SQL")]
    public async Task The_holding_flow_is_refused()
    {
        await using var db = await ReindexFixture.OpenAsync();
        await using var unit = await UnitOfWork.BeginAsync(db.Context);
        db.Sink.Clear();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Database, timeoutMs: 1000)).WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Contains("UnitOfWork", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(db.Sink.Maintenance);
    }

    [Fact(DisplayName = "AC-136-07 an external connection holding BEGIN IMMEDIATE makes REINDEX Busy within the timeout plus 2 s")]
    public async Task An_external_writer_is_busy()
    {
        await using var db = await ReindexFixture.OpenAsync();
        await using var blocker = await db.RawAsync();
        await using var immediate = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(blocker, "INSERT INTO b (k) VALUES ('blocker')", immediate);

        var clock = Stopwatch.StartNew();
        var result = await db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Table, timeoutMs: 200));
        clock.Stop();

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(200) + SqliteMaintenanceDatabase.Margin, "took " + clock.Elapsed);
        await immediate.CommitAsync();
    }
}

// AC-136-10 to AC-136-12: failures, cancellation, the result and diagnostics.
public sealed class ReindexFailureTests
{
    [Fact(DisplayName = "AC-136-10 an index dropped after its descriptor was generated propagates SQLite's error unwrapped; the gate is released")]
    public async Task A_missing_index_propagates()
    {
        await using var db = await ReindexFixture.OpenAsync();
        await db.ExecAsync("DROP INDEX ix_a_k_live");

        var failure = await Assert.ThrowsAsync<SqliteException>(() => db.Maintenance.ReindexAsync(
            new ReindexRequest(ReindexScope.Index, ReindexMode.Blocking) { Table = ReindexFixture.A, Index = ReindexFixture.Live }));

        Assert.Equal(1, failure.SqliteErrorCode);
        await db.WriteAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "AC-136-11 a pre-cancelled token runs no SQL; cancelling the gate wait releases everything; the next REINDEX and write succeed and the data is unchanged")]
    public async Task Cancellation()
    {
        await using var db = await ReindexFixture.OpenAsync();
        var before = await ReindexFixture.DumpAsync(db);
        var (context, source) = db.Counting();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        db.Sink.Clear();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Database), cancelled.Token));
        Assert.Equal(0, source.Opens);
        Assert.Empty(db.Sink.Maintenance);

        var holder = await GateHolder.HoldAsync(db.Context);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Table, timeoutMs: 30000), cancel.Token));
        await holder.CommitAsync();

        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Table, timeoutMs: 5000))).Status);
        await db.WriteAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(before, await ReindexFixture.DumpAsync(db));
    }

    [Fact(DisplayName = "AC-136-12 ReindexResult is a sealed IMaintenanceResult carrying scope, mode, table and index; one event tagged Reindex without connection text")]
    public async Task Result_and_diagnostics()
    {
        await using var db = await ReindexFixture.OpenAsync();
        db.Sink.Clear();

        var result = await db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Index));

        Assert.True(typeof(ReindexResult).IsSealed);
        Assert.True(typeof(IMaintenanceResult).IsAssignableFrom(typeof(ReindexResult)));
        Assert.Equal(MaintenanceOperation.Reindex, result.Operation);
        Assert.Equal("sqlite", result.Provider);
        Assert.Equal(ReindexScope.Index, result.Scope);
        Assert.Equal(ReindexMode.Blocking, result.Mode);
        Assert.Equal("a", result.Table);
        Assert.Equal("ix_a_u", result.Index);
        Assert.True(result.IsComplete);
        var statement = Assert.Single(db.Sink.Maintenance);
        Assert.Equal(nameof(MaintenanceOperation.Reindex), statement.Operation);
        Assert.Equal("REINDEX main.\"ix_a_u\"", statement.Text);
        Assert.DoesNotContain("Data Source", statement.Text, StringComparison.OrdinalIgnoreCase);
    }
}

// AC-136-12: the documentation.
public sealed class ReindexDocsTests
{
    [Fact(DisplayName = "AC-136-12 docs/maintenance.md has a Reindex section with scopes, modes, the provider table, locks per mode, the Busy rule, _ccnew cleanup and an example")]
    public void The_section()
    {
        var section = MaintenanceDocs.Section("Reindex");

        foreach (var phrase in new[] { "`Database`", "`Table`", "`Index`", "`Blocking`", "`Concurrent`", "ReindexConcurrently", "SHARE", "ACCESS EXCLUSIVE", "Busy", "_ccnew", "DROP INDEX CONCURRENTLY", "indisvalid", "AnalyzeAsync", "ReindexAsync(new ReindexRequest(" })
            Assert.Contains(phrase, section, StringComparison.Ordinal);
        Assert.True(MaintenanceDocs.Page.IndexOf("## Analyze", StringComparison.Ordinal) < MaintenanceDocs.Page.IndexOf("## Reindex", StringComparison.Ordinal));
    }
}
