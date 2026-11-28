using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// Fixtures built at run time from SQL: no binary file is committed.
internal static class IntegrityFixtures
{
    internal static readonly EntityDescriptor Child = Descriptors.Table("child");
    internal static readonly EntityDescriptor CleanChild = Descriptors.Table("clean_child");

    internal static async Task ForeignKeyViolationsAsync(SqliteMaintenanceDatabase db, int violations = 2)
    {
        await using var raw = await db.RawAsync();
        await SqliteMaintenanceDatabase.Exec(raw, "PRAGMA foreign_keys=OFF");
        await SqliteMaintenanceDatabase.Exec(raw, "CREATE TABLE parent (id INTEGER PRIMARY KEY)");
        await SqliteMaintenanceDatabase.Exec(raw, "CREATE TABLE child (id INTEGER PRIMARY KEY, parent_id INTEGER REFERENCES parent (id))");
        await SqliteMaintenanceDatabase.Exec(raw, "CREATE TABLE clean_child (id INTEGER PRIMARY KEY, parent_id INTEGER REFERENCES parent (id))");
        await SqliteMaintenanceDatabase.Exec(raw, "INSERT INTO parent (id) VALUES (1), (2)");
        await SqliteMaintenanceDatabase.Exec(raw, "INSERT INTO clean_child (id, parent_id) VALUES (1, 1), (2, 2)");
        await SqliteMaintenanceDatabase.Exec(raw, "INSERT INTO child (id, parent_id) VALUES (1, 1)");
        for (var i = 0; i < violations; i++)
            await SqliteMaintenanceDatabase.Exec(raw, "INSERT INTO child (id, parent_id) VALUES (" + (10 + i) + ", " + (900 + i) + ")");
    }

    // An index whose stored definition no longer matches its entries: its rows hold column a, its schema says b.
    internal static async Task CorruptIndexAsync(SqliteMaintenanceDatabase db, int rows = 20)
    {
        await using (var raw = await db.RawAsync())
        {
            await SqliteMaintenanceDatabase.Exec(raw, "CREATE TABLE t (a INTEGER, b INTEGER)");
            await SqliteMaintenanceDatabase.Exec(raw, "CREATE INDEX ix_t ON t (a)");
            for (var i = 1; i <= rows; i++)
                await SqliteMaintenanceDatabase.Exec(raw, "INSERT INTO t (a, b) VALUES (" + i + ", " + (1000 - i) + ")");
            await SqliteMaintenanceDatabase.Exec(raw, "PRAGMA writable_schema=ON");
            await SqliteMaintenanceDatabase.Exec(raw, "UPDATE sqlite_schema SET sql = 'CREATE INDEX ix_t ON t (b)' WHERE name = 'ix_t'");
            await SqliteMaintenanceDatabase.Exec(raw, "PRAGMA writable_schema=OFF");
        }
        SqliteConnection.ClearAllPools();
    }

    internal static IntegrityCheckRequest Request(IntegrityCheckMode mode, EntityDescriptor? table = null, int maxFindings = 100, int? timeoutMs = null) =>
        new(mode) { Table = table, MaxFindings = maxFindings, Timeout = timeoutMs is { } ms ? TimeSpan.FromMilliseconds(ms) : null };
}

// The rendered statements (ORML-138 plan, AC-138-03 and AC-138-05).
public sealed class IntegrityRenderTests
{
    [Theory(DisplayName = "AC-138-05 whole-database Full and Quick ask SQLite for MaxFindings + 1 rows; table forms and foreign_key_check name the quoted table")]
    [InlineData(IntegrityCheckMode.Full, null, 100, "PRAGMA main.integrity_check(101)")]
    [InlineData(IntegrityCheckMode.Quick, null, 3, "PRAGMA main.quick_check(4)")]
    [InlineData(IntegrityCheckMode.ForeignKeys, null, 100, "PRAGMA main.foreign_key_check")]
    [InlineData(IntegrityCheckMode.Full, "child", 100, "PRAGMA main.integrity_check(\"child\")")]
    [InlineData(IntegrityCheckMode.Quick, "child", 100, "PRAGMA main.quick_check(\"child\")")]
    [InlineData(IntegrityCheckMode.ForeignKeys, "child", 100, "PRAGMA main.foreign_key_check(\"child\")")]
    public void The_forms(IntegrityCheckMode mode, string? table, int maxFindings, string expected) =>
        Assert.Equal(expected, SqliteProvider.Instance.RenderIntegrityCheck(mode, table is null ? null : Descriptors.Table(table), maxFindings));
}

// AC-138-01: a clean database.
public sealed class IntegrityCleanTests
{
    [Theory(DisplayName = "AC-138-01 on a healthy file database every mode completes with no findings and one event")]
    [InlineData(IntegrityCheckMode.Full)]
    [InlineData(IntegrityCheckMode.Quick)]
    [InlineData(IntegrityCheckMode.ForeignKeys)]
    public async Task A_healthy_file_database(IntegrityCheckMode mode)
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await IntegrityFixtures.ForeignKeyViolationsAsync(db, 0);
        db.Sink.Clear();

        var result = await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(mode));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.Empty(result.Findings);
        Assert.False(result.Truncated);
        Assert.True(result.IsComplete);
        Assert.Equal(mode, result.Mode);
        var statement = Assert.Single(db.Sink.Maintenance);
        Assert.Equal(MaintenanceOperation.IntegrityCheck, statement.Operation);
        Assert.DoesNotContain("Data Source", statement.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(db.Path, statement.Text, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "AC-138-01 on an in-memory database every mode completes with no findings")]
    [InlineData(IntegrityCheckMode.Full)]
    [InlineData(IntegrityCheckMode.Quick)]
    [InlineData(IntegrityCheckMode.ForeignKeys)]
    public async Task A_healthy_memory_database(IntegrityCheckMode mode)
    {
        await using var db = await SqliteMaintenanceDatabase.OpenMemoryAsync();

        var result = await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(mode));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.Empty(result.Findings);
        Assert.Single(db.Sink.Maintenance);
    }
}

// AC-138-02 and AC-138-03: foreign-key violations.
public sealed class IntegrityForeignKeyTests
{
    [Fact(DisplayName = "AC-138-02 ForeignKeys reports both violations with every field; Full and Quick do not report them")]
    public async Task Violations_are_reported_only_by_foreign_keys()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await IntegrityFixtures.ForeignKeyViolationsAsync(db);

        var keys = await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.ForeignKeys));
        var full = await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.Full));
        var quick = await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.Quick));

        Assert.Equal(MaintenanceStatus.IssuesFound, keys.Status);
        Assert.False(keys.IsComplete);
        Assert.Equal(2, keys.Findings.Count);
        Assert.Equal([10L, 11L], keys.Findings.Select(f => f.RowId!.Value).Order());
        Assert.All(keys.Findings, f =>
        {
            Assert.Equal(IntegrityCheckMode.ForeignKeys, f.Kind);
            Assert.Equal("child", f.Table);
            Assert.Equal("parent", f.ReferencedTable);
            Assert.NotNull(f.ForeignKeyId);
            Assert.Equal("child row " + f.RowId + " references a missing row in parent (foreign key " + f.ForeignKeyId + ")", f.Message);
        });
        Assert.Equal(MaintenanceStatus.Completed, full.Status);
        Assert.Equal(MaintenanceStatus.Completed, quick.Status);
    }

    [Fact(DisplayName = "AC-138-02 the result is the same whether the checking connection enforces foreign keys or not")]
    public async Task Enforcement_on_the_checking_connection_does_not_matter()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await IntegrityFixtures.ForeignKeyViolationsAsync(db);
        Assert.Contains("Foreign Keys=True", db.Context.DataSource.ConnectionString, StringComparison.OrdinalIgnoreCase);
        var off = new OrmContext(SqliteFactory.Instance.CreateDataSource(db.RawConnectionString + ";Foreign Keys=False"), SqliteProvider.Instance);

        var enforced = await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.ForeignKeys));
        var relaxed = await off.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.ForeignKeys));

        Assert.Equal(enforced.Findings, relaxed.Findings);
        Assert.Equal(2, relaxed.Findings.Count);
    }

    [Fact(DisplayName = "AC-138-03 a table-scoped check reports the clean table Completed and the violating one IssuesFound with 2 findings")]
    public async Task The_table_scope()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await IntegrityFixtures.ForeignKeyViolationsAsync(db);
        db.Sink.Clear();

        var clean = await db.Maintenance.CheckIntegrityAsync(IntegrityFixtures.Request(IntegrityCheckMode.ForeignKeys, IntegrityFixtures.CleanChild));
        var violating = await db.Maintenance.CheckIntegrityAsync(IntegrityFixtures.Request(IntegrityCheckMode.ForeignKeys, IntegrityFixtures.Child));
        var fullTable = await db.Maintenance.CheckIntegrityAsync(IntegrityFixtures.Request(IntegrityCheckMode.Full, IntegrityFixtures.Child));

        Assert.Equal(MaintenanceStatus.Completed, clean.Status);
        Assert.Equal(MaintenanceStatus.IssuesFound, violating.Status);
        Assert.Equal(2, violating.Findings.Count);
        Assert.Equal(MaintenanceStatus.Completed, fullTable.Status);
        Assert.Equal("PRAGMA main.foreign_key_check(\"clean_child\")", db.Sink.Maintenance[0].Text);
    }

    [Fact(DisplayName = "AC-138-02 a WITHOUT ROWID table's finding has no rowid and its message omits it")]
    public async Task A_without_rowid_table()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await using (var raw = await db.RawAsync())
        {
            await SqliteMaintenanceDatabase.Exec(raw, "PRAGMA foreign_keys=OFF");
            await SqliteMaintenanceDatabase.Exec(raw, "CREATE TABLE parent (id INTEGER PRIMARY KEY)");
            await SqliteMaintenanceDatabase.Exec(raw, "CREATE TABLE keyed (code TEXT PRIMARY KEY, parent_id INTEGER REFERENCES parent (id)) WITHOUT ROWID");
            await SqliteMaintenanceDatabase.Exec(raw, "INSERT INTO keyed VALUES ('x', 42)");
        }

        var finding = Assert.Single((await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.ForeignKeys))).Findings);

        Assert.Null(finding.RowId);
        Assert.Equal("keyed references a missing row in parent (foreign key 0)", finding.Message);
    }
}

// AC-138-04: a structural problem.
public sealed class IntegrityStructuralTests
{
    [Fact(DisplayName = "AC-138-04 Full reports an index that no longer matches its table with SQLite's own messages; Quick's outcome is recorded; the file is byte-identical")]
    public async Task A_corrupt_index()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        await IntegrityFixtures.CorruptIndexAsync(db);
        var before = db.FileHash();

        var full = await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.Full));
        var quick = await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.Quick));

        Assert.Equal(MaintenanceStatus.IssuesFound, full.Status);
        Assert.All(full.Findings, f =>
        {
            Assert.Equal(IntegrityCheckMode.Full, f.Kind);
            Assert.False(string.IsNullOrWhiteSpace(f.Message));
            Assert.Null(f.Table);
        });
        Assert.Contains(full.Findings, f => f.Message.Contains("ix_t", StringComparison.Ordinal));
        // Recorded: quick_check does not compare an index with its table, so this corruption is invisible to it.
        Assert.Equal(MaintenanceStatus.Completed, quick.Status);
        SqliteConnection.ClearAllPools();
        Assert.Equal(before, db.FileHash());
    }
}

// AC-138-05: bounded findings.
public sealed class IntegrityBoundingTests
{
    [Theory(DisplayName = "AC-138-05 with 5 foreign-key violations MaxFindings 3 returns 3 truncated, 5 returns 5, 10 returns 5")]
    [InlineData(3, 3, true)]
    [InlineData(5, 5, false)]
    [InlineData(10, 5, false)]
    public async Task Foreign_key_findings_are_bounded(int max, int count, bool truncated)
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await IntegrityFixtures.ForeignKeyViolationsAsync(db, 5);

        var result = await db.Maintenance.CheckIntegrityAsync(IntegrityFixtures.Request(IntegrityCheckMode.ForeignKeys, maxFindings: max));

        Assert.Equal(count, result.Findings.Count);
        Assert.Equal(truncated, result.Truncated);
        Assert.Equal(MaintenanceStatus.IssuesFound, result.Status);
    }

    [Fact(DisplayName = "AC-138-05 the same bounding applies to Full")]
    public async Task Full_findings_are_bounded()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        await IntegrityFixtures.CorruptIndexAsync(db);
        var all = await db.Maintenance.CheckIntegrityAsync(IntegrityFixtures.Request(IntegrityCheckMode.Full, maxFindings: IntegrityCheckRequest.MaxFindingsLimit));
        Assert.True(all.Findings.Count > 3);
        Assert.False(all.Truncated);

        var three = await db.Maintenance.CheckIntegrityAsync(IntegrityFixtures.Request(IntegrityCheckMode.Full, maxFindings: 3));
        var exact = await db.Maintenance.CheckIntegrityAsync(IntegrityFixtures.Request(IntegrityCheckMode.Full, maxFindings: all.Findings.Count));

        Assert.Equal(3, three.Findings.Count);
        Assert.True(three.Truncated);
        Assert.Equal(all.Findings.Take(3), three.Findings);
        Assert.Equal(all.Findings.Count, exact.Findings.Count);
        Assert.False(exact.Truncated);
    }
}

// AC-138-06 and AC-138-07: contention, and a check inside a unit of work.
public sealed class IntegrityContentionTests
{
    [Fact(DisplayName = "AC-138-06 another connection holding BEGIN EXCLUSIVE with a pending write makes Full Busy within the timeout plus 2 s; after it commits Full completes")]
    public async Task An_exclusive_lock_is_busy()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        await using var blocker = await db.RawAsync();
        await SqliteMaintenanceDatabase.Exec(blocker, "BEGIN EXCLUSIVE");
        await SqliteMaintenanceDatabase.Exec(blocker, "INSERT INTO seed DEFAULT VALUES");

        var clock = Stopwatch.StartNew();
        var result = await db.Maintenance.CheckIntegrityAsync(IntegrityFixtures.Request(IntegrityCheckMode.Full, timeoutMs: 200));
        clock.Stop();

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Empty(result.Findings);
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(200) + SqliteMaintenanceDatabase.Margin, "took " + clock.Elapsed);
        await SqliteMaintenanceDatabase.Exec(blocker, "COMMIT");
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.CheckIntegrityAsync(IntegrityFixtures.Request(IntegrityCheckMode.Full, timeoutMs: 5000))).Status);
    }
}

public sealed class IntegrityUnitOfWorkTests
{
    [Fact(DisplayName = "AC-138-07 inside an open unit of work with uncommitted writes the check is not refused, does not deadlock, sees committed data only, and the unit commits")]
    public async Task Inside_a_unit_of_work()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await IntegrityFixtures.ForeignKeyViolationsAsync(db);
        var unit = await UnitOfWork.BeginAsync(db.Context);
        await SqliteMaintenanceDatabase.Exec(unit.Connection, "DELETE FROM child WHERE id = 10", unit.Transaction);

        var inside = await db.Maintenance.CheckIntegrityAsync(IntegrityFixtures.Request(IntegrityCheckMode.ForeignKeys, timeoutMs: 2000))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(2, inside.Findings.Count);
        await unit.CommitAsync();
        await unit.DisposeAsync();
        Assert.Single((await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.ForeignKeys))).Findings);
    }
}

// AC-138-08: an unreadable database.
public sealed class IntegrityFailureTests
{
    [Fact(DisplayName = "AC-138-08 a file that is not a SQLite database throws the original SqliteException, not IssuesFound, and releases the connection")]
    public async Task Not_a_database()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        SqliteConnection.ClearAllPools();
        var sound = await File.ReadAllBytesAsync(db.Path);
        await File.WriteAllTextAsync(db.Path, new string('x', 8192));

        var failure = await Assert.ThrowsAsync<SqliteException>(() => db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.Full)));

        Assert.Equal(26, failure.SqliteErrorCode);
        SqliteConnection.ClearAllPools();
        await File.WriteAllBytesAsync(db.Path, sound);
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.Full))).Status);
    }
}

// AC-138-09 to AC-138-11: refusals and validation.
public sealed class IntegrityRefusalTests
{
    [Theory(DisplayName = "AC-138-09 PostgreSQL refuses every mode with IntegrityCheck before any connection")]
    [InlineData(IntegrityCheckMode.Full)]
    [InlineData(IntegrityCheckMode.Quick)]
    [InlineData(IntegrityCheckMode.ForeignKeys)]
    public async Task Postgres_is_refused(IntegrityCheckMode mode)
    {
        var context = new OrmContext(new NeverUsedDataSource(), PostgresProvider.Instance);

        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(mode)));

        Assert.Equal("postgres", refusal.Provider);
        Assert.Equal(nameof(OrmMaintenance.CheckIntegrityAsync), refusal.Node);
        Assert.Equal(Capability.IntegrityCheck, refusal.Capability);
    }

    [Fact(DisplayName = "AC-138-10 a provider without the port is refused the same way")]
    public async Task A_portless_provider_is_refused()
    {
        var context = new OrmContext(new NeverUsedDataSource(), new PortlessProvider());

        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => context.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.Full)));

        Assert.Equal(Capability.IntegrityCheck, refusal.Capability);
    }

    public static TheoryData<IntegrityCheckRequest?, Type> Invalid() => new()
    {
        { null, typeof(ArgumentNullException) },
        { new IntegrityCheckRequest((IntegrityCheckMode)0), typeof(ArgumentOutOfRangeException) },
        { new IntegrityCheckRequest((IntegrityCheckMode)8), typeof(ArgumentOutOfRangeException) },
        { new IntegrityCheckRequest(IntegrityCheckMode.Full) { MaxFindings = 0 }, typeof(ArgumentOutOfRangeException) },
        { new IntegrityCheckRequest(IntegrityCheckMode.Full) { MaxFindings = IntegrityCheckRequest.MaxFindingsLimit + 1 }, typeof(ArgumentOutOfRangeException) },
        { new IntegrityCheckRequest(IntegrityCheckMode.Full) { Timeout = TimeSpan.Zero }, typeof(ArgumentOutOfRangeException) },
    };

    [Theory(DisplayName = "AC-138-11 an invalid request throws before the capability check")]
    [MemberData(nameof(Invalid))]
    public async Task Invalid_requests_are_refused(IntegrityCheckRequest? request, Type expected)
    {
        var context = new OrmContext(new NeverUsedDataSource(), new PortlessProvider());

        var thrown = await Assert.ThrowsAnyAsync<ArgumentException>(() => context.Maintenance.CheckIntegrityAsync(request!));

        Assert.IsType(expected, thrown);
    }

    [Fact(DisplayName = "AC-138-11 MaxFindings defaults to 100 and is bounded at 10,000; the mode enum has no zero member")]
    public void The_defaults()
    {
        Assert.Equal(100, new IntegrityCheckRequest(IntegrityCheckMode.Full).MaxFindings);
        Assert.Equal(10_000, IntegrityCheckRequest.MaxFindingsLimit);
        Assert.Equal([1, 2, 3], Enum.GetValues<IntegrityCheckMode>().Select(m => (int)m));
    }
}

// AC-138-12 and AC-138-13: cancellation and the appended members.
public sealed class IntegrityCancellationTests
{
    [Fact(DisplayName = "AC-138-12 a pre-cancelled token throws with no connection and no event")]
    public async Task A_cancelled_token_runs_nothing()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        var (context, source) = db.Counting();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        db.Sink.Clear();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.Full), cancelled.Token));

        Assert.Equal(0, source.Opens);
        Assert.Empty(db.Sink.Maintenance);
    }

    [Fact(DisplayName = "AC-138-12 a token cancelled from the command-start hook throws; the connection is returned and the next call succeeds")]
    public async Task Cancelled_at_command_start()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        using var hook = new StatementStartHook();
        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            StatementStartHook.OnStart(cancel.Cancel);
            await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.Full), cancel.Token);
        });

        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.CheckIntegrityAsync(new IntegrityCheckRequest(IntegrityCheckMode.Full))).Status);
    }
}

public sealed class IntegrityCapabilityTests
{
    [Fact(DisplayName = "AC-138-13 IntegrityCheck follows ReindexConcurrently; IntegrityCheck and IssuesFound are appended; IsComplete is false for IssuesFound")]
    public void The_members_are_appended()
    {
        Assert.Equal(26, (int)Capability.ReindexConcurrently);
        Assert.Equal(27, (int)Capability.IntegrityCheck);
        Assert.Equal(5, (int)MaintenanceOperation.IntegrityCheck);
        Assert.Equal(5, (int)MaintenanceStatus.NotRun);
        Assert.Equal(6, (int)MaintenanceStatus.IssuesFound);
        Assert.Equal(Support.Native, SqliteProvider.Instance.Capabilities.Of(Capability.IntegrityCheck));
        Assert.Equal(Support.None, PostgresProvider.Instance.Capabilities.Of(Capability.IntegrityCheck));
        Assert.False(new IntegrityCheckResult("sqlite", IntegrityCheckMode.Full, MaintenanceStatus.IssuesFound, [new IntegrityFinding(IntegrityCheckMode.Full, "x")], false).IsComplete);
        Assert.Equal(MaintenanceOperation.IntegrityCheck, new IntegrityCheckResult("sqlite", IntegrityCheckMode.Full, MaintenanceStatus.Completed, [], false).Operation);
    }
}

// AC-138-14: the documentation.
public sealed class IntegrityDocsTests
{
    [Fact(DisplayName = "AC-138-14 docs/maintenance.md has an Integrity checks section comparing the three checks and naming amcheck and pg_amcheck")]
    public void The_section()
    {
        var section = MaintenanceDocs.Section("Integrity checks");

        foreach (var phrase in new[] { "`Full`", "`Quick`", "`ForeignKeys`", "ONLY by `ForeignKeys`", "foreign_keys=OFF", "MaxFindings", "Truncated", "IssuesFound", "SQLITE_NOTADB", "read-only", "no writer gate", "amcheck", "pg_amcheck", "3.33" })
            Assert.Contains(phrase, section, StringComparison.Ordinal);
    }
}
