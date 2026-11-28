using System.Diagnostics;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

internal static class AnalyzeFixture
{
    internal static readonly EntityDescriptor A = Descriptors.Table("a");
    internal static readonly EntityDescriptor B = Descriptors.Table("b");

    internal static async Task<SqliteMaintenanceDatabase> OpenAsync()
    {
        var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await SeedAsync(db.ExecAsync);
        return db;
    }

    internal static async Task SeedAsync(Func<string, Task> exec)
    {
        foreach (var table in new[] { "a", "b" })
        {
            await exec("CREATE TABLE " + table + " (id INTEGER PRIMARY KEY, k TEXT NOT NULL)");
            await exec("CREATE INDEX ix_" + table + "_k ON " + table + " (k)");
            for (var i = 0; i < 30; i++) await exec("INSERT INTO " + table + " (k) VALUES ('k" + (i % 7) + "')");
        }
    }

    internal static Task<List<string>> DumpAsync(SqliteMaintenanceDatabase db) =>
        db.DumpAsync("SELECT 'a', id, k FROM a UNION ALL SELECT 'b', id, k FROM b ORDER BY 1, 2");

    internal static AnalyzeRequest Table(EntityDescriptor table, int? timeoutMs = null) =>
        new(AnalyzeScope.Table) { Table = table, Timeout = timeoutMs is { } ms ? TimeSpan.FromMilliseconds(ms) : null };
}

// AC-135-01: an explicit scope, rendered from the scope and the descriptor only; no SQL overload anywhere.
public sealed class AnalyzeRenderTests
{
    [Fact(DisplayName = "AC-135-01 SQLite renders ANALYZE main and ANALYZE main.<quoted table>; PostgreSQL ANALYZE and ANALYZE <quoted table>")]
    public void The_forms()
    {
        Assert.Equal("ANALYZE main", SqliteProvider.Instance.RenderAnalyze(AnalyzeScope.Database, null));
        Assert.Equal("ANALYZE main.\"a\"", SqliteProvider.Instance.RenderAnalyze(AnalyzeScope.Table, AnalyzeFixture.A));
        Assert.Equal("ANALYZE main.\"we\"\"ird\"", SqliteProvider.Instance.RenderAnalyze(AnalyzeScope.Table, Descriptors.Table("we\"ird")));
        Assert.Equal("ANALYZE", PostgresProvider.Instance.RenderAnalyze(AnalyzeScope.Database, null));
        Assert.Equal("ANALYZE \"a\"", PostgresProvider.Instance.RenderAnalyze(AnalyzeScope.Table, AnalyzeFixture.A));
        Assert.Throws<ArgumentOutOfRangeException>(() => SqliteProvider.Instance.RenderAnalyze(AnalyzeScope.Table, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => PostgresProvider.Instance.RenderAnalyze((AnalyzeScope)0, null));
    }

    [Fact(DisplayName = "AC-135-01 AnalyzeScope has Database = 1 and Table, and no zero member")]
    public void The_scope_is_explicit()
    {
        Assert.Equal([1, 2], Enum.GetValues<AnalyzeScope>().Select(s => (int)s));
        Assert.False(Enum.IsDefined((AnalyzeScope)0));
    }

    [Fact(DisplayName = "AC-135-01 no maintenance method takes SQL: each public operation takes one request record and a token")]
    public void No_method_takes_sql()
    {
        var operations = typeof(OrmMaintenance).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotEmpty(operations);
        Assert.All(operations, method =>
        {
            var parameters = method.GetParameters();
            Assert.Equal(2, parameters.Length);
            Assert.EndsWith("Request", parameters[0].ParameterType.Name, StringComparison.Ordinal);
            Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
            Assert.DoesNotContain(parameters[0].ParameterType.GetProperties(), p => p.PropertyType == typeof(string));
        });
    }
}

// AC-135-02: validation before anything else.
public sealed class AnalyzeValidationTests
{
    public static TheoryData<AnalyzeRequest?, Type> Invalid() => new()
    {
        { null, typeof(ArgumentNullException) },
        { new AnalyzeRequest((AnalyzeScope)0), typeof(ArgumentOutOfRangeException) },
        { new AnalyzeRequest((AnalyzeScope)9), typeof(ArgumentOutOfRangeException) },
        { new AnalyzeRequest(AnalyzeScope.Table), typeof(ArgumentException) },
        { new AnalyzeRequest(AnalyzeScope.Database) { Table = AnalyzeFixture.A }, typeof(ArgumentException) },
        { new AnalyzeRequest(AnalyzeScope.Table) { Table = EntityDescriptor.Of("FixtureV", "v", true).View() }, typeof(ArgumentException) },
        { new AnalyzeRequest(AnalyzeScope.Database) { Timeout = TimeSpan.Zero }, typeof(ArgumentOutOfRangeException) },
        { new AnalyzeRequest(AnalyzeScope.Database) { Timeout = TimeSpan.FromSeconds(-1) }, typeof(ArgumentOutOfRangeException) },
    };

    [Theory(DisplayName = "AC-135-02 an invalid request throws before the capability check and before any connection")]
    [MemberData(nameof(Invalid))]
    public async Task Invalid_requests_are_refused(AnalyzeRequest? request, Type expected)
    {
        foreach (var provider in new ISqlProvider[] { SqliteProvider.Instance, new PortlessProvider() })
        {
            var context = new OrmContext(new NeverUsedDataSource(), provider);

            var thrown = await Assert.ThrowsAnyAsync<ArgumentException>(() => context.Maintenance.AnalyzeAsync(request!));

            Assert.IsType(expected, thrown);
        }
    }
}

// AC-135-03 and AC-135-05: the SQLite demonstration and the result shape.
public sealed class AnalyzeSqliteTests
{
    [Fact(DisplayName = "AC-135-03 Table scope analyzes only A, Database scope analyzes A and B, and no row changes")]
    public async Task Table_then_database()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        var before = await AnalyzeFixture.DumpAsync(db);

        var table = await db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A));
        var afterTable = await db.DumpAsync("SELECT DISTINCT tbl FROM sqlite_stat1 ORDER BY tbl");
        var database = await db.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database));

        Assert.Equal(MaintenanceStatus.Completed, table.Status);
        Assert.True(table.IsComplete);
        Assert.Equal(["a"], afterTable);
        Assert.Equal(MaintenanceStatus.Completed, database.Status);
        Assert.Equal(["a", "b"], await db.DumpAsync("SELECT DISTINCT tbl FROM sqlite_stat1 ORDER BY tbl"));
        Assert.Equal(before, await AnalyzeFixture.DumpAsync(db));
    }

    [Fact(DisplayName = "AC-135-05 AnalyzeResult is a sealed record implementing IMaintenanceResult with Operation, Provider, Scope, Table, Status and IsComplete")]
    public async Task The_result_shape()
    {
        await using var db = await AnalyzeFixture.OpenAsync();

        var table = await db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A));
        var database = await db.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database));

        Assert.True(typeof(AnalyzeResult).IsSealed);
        Assert.True(typeof(IMaintenanceResult).IsAssignableFrom(typeof(AnalyzeResult)));
        Assert.Equal(MaintenanceOperation.Analyze, table.Operation);
        Assert.Equal(3, (int)MaintenanceOperation.Analyze);
        Assert.Equal("sqlite", table.Provider);
        Assert.Equal(AnalyzeScope.Table, table.Scope);
        Assert.Equal("a", table.Table);
        Assert.Null(database.Table);
        Assert.False(new AnalyzeResult("sqlite", AnalyzeScope.Database, null, MaintenanceStatus.Busy).IsComplete);
    }
}

// AC-135-06 and AC-135-07: contention, the gate and the holder on SQLite.
public sealed class AnalyzeSqliteGateTests
{
    [Fact(DisplayName = "AC-135-06 an external connection holding BEGIN IMMEDIATE makes ANALYZE Busy within the timeout plus 2 s; the blocker commits normally")]
    public async Task An_external_writer_is_busy()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await using var blocker = await db.RawAsync();
        await using var immediate = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(blocker, "INSERT INTO a (k) VALUES ('blocker')", immediate);

        var clock = Stopwatch.StartNew();
        var result = await db.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database) { Timeout = TimeSpan.FromMilliseconds(200) });
        clock.Stop();

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(200) + SqliteMaintenanceDatabase.Margin, "took " + clock.Elapsed);
        await immediate.CommitAsync();
        Assert.Equal(1L, await db.LongAsync("SELECT count(*) FROM a WHERE k = 'blocker'"));
    }

    [Fact(DisplayName = "AC-135-07 a gate held by another flow's unit of work gives Busy without opening a connection")]
    public async Task A_busy_gate_opens_nothing()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        var (context, source) = db.Counting();
        await using var holder = await GateHolder.HoldAsync(db.Context);

        var result = await context.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database) { Timeout = TimeSpan.FromMilliseconds(200) });

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Equal(0, source.Opens);
    }

    [Fact(DisplayName = "AC-135-07 inside an open unit of work on the same database ANALYZE is refused before SQL")]
    public async Task The_holding_flow_is_refused()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await using var unit = await UnitOfWork.BeginAsync(db.Context);
        db.Sink.Clear();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database) { Timeout = TimeSpan.FromSeconds(1) }).WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Contains("UnitOfWork", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(db.Sink.Maintenance);
    }

    [Fact(DisplayName = "AC-135-07 an ORM write on the same database issued during ANALYZE waits and then completes")]
    public async Task A_write_waits_for_analyze()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await db.ExecAsync(WalFixture.Items);
        using var hook = new StatementStartHook();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var analyze = Task.Run(async () =>
        {
            StatementStartHook.OnStart(() => { started.Set(); release.Wait(TimeSpan.FromSeconds(20)); });
            return await db.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database) { Timeout = TimeSpan.FromSeconds(20) });
        });
        Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
        var write = Task.Run(() => db.WriteAsync());
        await Task.Delay(300);
        Assert.False(write.IsCompleted, "the write did not wait for ANALYZE's gate");

        release.Set();
        Assert.Equal(MaintenanceStatus.Completed, (await analyze).Status);
        await write.WaitAsync(TimeSpan.FromSeconds(10));
    }
}

// AC-135-08: an unsupported provider.
public sealed class AnalyzeRefusalTests
{
    [Fact(DisplayName = "AC-135-08 a provider without the port, and one that declares Analyze None, are refused with Analyze before any connection")]
    public async Task Unsupported_providers_are_refused()
    {
        foreach (var provider in new ISqlProvider[] { new PortlessProvider(), TunedSqliteProvider.Without(Capability.Analyze) })
        {
            var context = new OrmContext(new NeverUsedDataSource(), provider);

            var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database)));

            Assert.Equal(Capability.Analyze, refusal.Capability);
            Assert.Equal(nameof(OrmMaintenance.AnalyzeAsync), refusal.Node);
            Assert.Equal(provider.Id, refusal.Provider);
        }
    }

    [Fact(DisplayName = "AC-135-08 Analyze is appended after VacuumTable and is Native on both providers")]
    public void The_capability_is_appended()
    {
        Assert.Equal(23, (int)Capability.VacuumTable);
        Assert.Equal(24, (int)Capability.Analyze);
        Assert.Equal(Support.Native, SqliteProvider.Instance.Capabilities.Of(Capability.Analyze));
        Assert.Equal(Support.Native, PostgresProvider.Instance.Capabilities.Of(Capability.Analyze));
    }
}

// AC-135-09 to AC-135-11: failures, cancellation and diagnostics.
public sealed class AnalyzeFailureTests
{
    [Fact(DisplayName = "AC-135-09 a table that does not exist propagates SQLite's own error unwrapped, and the connection and gate are released")]
    public async Task A_missing_table_propagates()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await db.ExecAsync(WalFixture.Items);

        var failure = await Assert.ThrowsAsync<SqliteException>(() => db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(Descriptors.Table("no_such_table"))));

        Assert.Equal(1, failure.SqliteErrorCode);
        Assert.Contains("no such table", failure.Message, StringComparison.OrdinalIgnoreCase);
        await db.WriteAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A))).Status);
    }

    [Fact(DisplayName = "AC-135-10 a pre-cancelled token runs no SQL; cancelling the gate wait releases everything and the next ANALYZE and write succeed")]
    public async Task Cancellation()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await db.ExecAsync(WalFixture.Items);
        var (context, source) = db.Counting();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        db.Sink.Clear();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database), cancelled.Token));
        Assert.Equal(0, source.Opens);
        Assert.Empty(db.Sink.Maintenance);

        var holder = await GateHolder.HoldAsync(db.Context);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => db.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database) { Timeout = TimeSpan.FromSeconds(30) }, cancel.Token));
        await holder.CommitAsync();

        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database) { Timeout = TimeSpan.FromSeconds(5) })).Status);
        await db.WriteAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "AC-135-11 one Maintenance event per call, tagged Analyze, with no connection-string text")]
    public async Task One_event()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        db.Sink.Clear();

        await db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A));

        var statement = Assert.Single(db.Sink.Maintenance);
        Assert.Equal(MaintenanceOperation.Analyze, statement.Operation);
        Assert.Equal("ANALYZE main.\"a\"", statement.Text);
        Assert.DoesNotContain("Data Source", statement.Text, StringComparison.OrdinalIgnoreCase);
    }
}

// AC-135-11: the documentation.
public sealed class AnalyzeDocsTests
{
    [Fact(DisplayName = "AC-135-11 docs/maintenance.md has an Analyze section after VACUUM with scopes, the provider table, locks, the Busy rule, the privilege caveat and an example")]
    public void The_section()
    {
        var section = MaintenanceDocs.Section("Analyze");

        foreach (var phrase in new[] { "`Database`", "`Table`", "no index scope", "PRAGMA optimize", "writer gate", "SHARE UPDATE EXCLUSIVE", "Busy", "55P03", "WARNING", "AnalyzeAsync(" })
            Assert.Contains(phrase, section, StringComparison.Ordinal);
        Assert.True(MaintenanceDocs.Page.IndexOf("## VACUUM", StringComparison.Ordinal) < MaintenanceDocs.Page.IndexOf("## Analyze", StringComparison.Ordinal));
    }
}
