using System.Diagnostics;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.Nodes;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// AC-134-07: the four PostgreSQL forms, rendered from the enum and the descriptor only. Needs no server.
public sealed class PostgresVacuumRenderTests
{
    [Theory(DisplayName = "AC-134-07 PostgreSQL renders VACUUM, VACUUM <table>, VACUUM (FULL) and VACUUM (FULL) <table>, quoting the table as every statement does")]
    [InlineData(VacuumMode.Plain, null, "VACUUM")]
    [InlineData(VacuumMode.Plain, "notes", "VACUUM \"notes\"")]
    [InlineData(VacuumMode.Full, null, "VACUUM (FULL)")]
    [InlineData(VacuumMode.Full, "notes", "VACUUM (FULL) \"notes\"")]
    [InlineData(VacuumMode.Plain, "app.notes", "VACUUM \"app.notes\"")]
    [InlineData(VacuumMode.Full, "we\"ird", "VACUUM (FULL) \"we\"\"ird\"")]
    public void The_four_forms(VacuumMode mode, string? table, string expected)
    {
        var descriptor = table is null ? null : Descriptors.Table(table);

        var text = PostgresProvider.Instance.RenderVacuum(mode, descriptor);

        Assert.Equal(expected, text);
        if (table is not null) Assert.EndsWith(Descriptors.Quoted(PostgresProvider.Instance, table), text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC-134-07 Plain never contains FULL, and an undefined mode is refused")]
    public void Plain_never_escalates()
    {
        Assert.DoesNotContain("FULL", PostgresProvider.Instance.RenderVacuum(VacuumMode.Plain, null), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FULL", PostgresProvider.Instance.RenderVacuum(VacuumMode.Plain, Descriptors.Table("full")).Replace("\"full\"", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
        Assert.Throws<ArgumentOutOfRangeException>(() => PostgresProvider.Instance.RenderVacuum((VacuumMode)7, null));
    }
}

// The shared PostgreSQL port members 134 ships. Needs no server.
public sealed class PostgresMaintenanceKeyTests
{
    [Fact(DisplayName = "the PostgreSQL writer key is postgres:host:port/database and carries no credential or other keyword")]
    public void The_key_carries_no_secret()
    {
        using var source = NpgsqlDataSource.Create("Host=db.internal;Port=6543;Database=app;Username=owner;Password=s3cret-pw;Application Name=probe");

        var key = PostgresProvider.Instance.WriterKeyOf(source);

        Assert.Equal("postgres:db.internal:6543/app", key);
        Assert.Equal(key, PostgresProvider.Instance.WriterKeyOf(source));
        Assert.DoesNotContain("s3cret", key, StringComparison.Ordinal);
        Assert.DoesNotContain("owner", key, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "PostgreSQL has no in-process gate: the lease is entered and holds nothing")]
    public async Task The_lease_is_a_no_op()
    {
        using var source = NpgsqlDataSource.Create("Host=localhost;Database=app");

        var lease = await PostgresProvider.Instance.TryEnterWriteAsync(source, TimeSpan.Zero, CancellationToken.None);

        Assert.True(lease.Entered);
        Assert.Null(lease.Release);
        Assert.False(((IMaintenanceProvider)PostgresProvider.Instance).HasWriterGate);
        Assert.Equal(TimeSpan.FromSeconds(30), PostgresProvider.DefaultMaintenanceTimeout);
        Assert.Equal(PostgresProvider.DefaultMaintenanceTimeout, PostgresProvider.Instance.MaintenanceTimeout);
    }

    [Theory(DisplayName = "the PostgreSQL lock guard sets lock_timeout to the remaining budget in whole milliseconds, at least 1, and resets it")]
    [InlineData(1500.0, "SET lock_timeout = '1500ms'")]
    [InlineData(0.2, "SET lock_timeout = '1ms'")]
    [InlineData(0.0, "SET lock_timeout = '1ms'")]
    [InlineData(30000.0, "SET lock_timeout = '30000ms'")]
    public void The_lock_guard(double milliseconds, string setup)
    {
        var guard = PostgresProvider.Instance.RenderLockGuard(TimeSpan.FromMilliseconds(milliseconds));

        Assert.NotNull(guard);
        Assert.Equal(setup, guard!.Setup);
        Assert.Equal("RESET lock_timeout", guard.Teardown);
    }

    [Theory(DisplayName = "PostgreSQL reports SQLSTATE 55P03 as contention and nothing else")]
    [InlineData("55P03", true)]
    [InlineData("57014", false)]
    [InlineData("40P01", false)]
    [InlineData("42P01", false)]
    public void Postgres_contention(string sqlState, bool contention) =>
        Assert.Equal(contention, PostgresProvider.Instance.IsContention(new PostgresException("probe", "ERROR", "ERROR", sqlState)));
}

internal static class PostgresVacuumFixture
{
    internal static async Task SeedAsync(PostgresMaintenanceDatabase db, string table = "vac_notes")
    {
        await db.ExecAsync("CREATE TABLE " + table + " (id integer PRIMARY KEY, v text NOT NULL) WITH (autovacuum_enabled = false)");
        await db.ExecAsync("INSERT INTO " + table + " SELECT g, repeat('p', 200) FROM generate_series(1, 2000) g");
        await db.ExecAsync("DELETE FROM " + table + " WHERE id % 2 = 0");
    }
}

// AC-134-08: plain and FULL against a server.
public sealed class PostgresVacuumExecutionTests
{
    [PostgresFact(DisplayName = "AC-134-08 Plain of the database and of one table completes and keeps the relfilenode; FULL of the table rewrites it and keeps the rows; one event each, no transaction")]
    public async Task Plain_and_full()
    {
        await using var db = await PostgresMaintenanceDatabase.CreateAsync();
        await PostgresVacuumFixture.SeedAsync(db);
        var table = Descriptors.Table("vac_notes");
        var rows = await db.DumpAsync("SELECT id, v FROM vac_notes ORDER BY id");
        var before = await db.RelFileNodeAsync("vac_notes");
        db.Sink.Clear();

        var whole = await db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain));
        var plain = await db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain) { Table = table });
        var kept = await db.RelFileNodeAsync("vac_notes");
        var full = await db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Full) { Table = table });

        Assert.Equal(MaintenanceStatus.Completed, whole.Status);
        Assert.Null(whole.Checkpoint);
        Assert.Equal(MaintenanceStatus.Completed, plain.Status);
        Assert.Equal("vac_notes", plain.Table);
        Assert.Equal(before, kept);
        Assert.Equal(MaintenanceStatus.Completed, full.Status);
        Assert.NotEqual(kept, await db.RelFileNodeAsync("vac_notes"));
        Assert.Equal(rows, await db.DumpAsync("SELECT id, v FROM vac_notes ORDER BY id"));
        Assert.Equal(["VACUUM", "VACUUM \"vac_notes\"", "VACUUM (FULL) \"vac_notes\""], db.Sink.Maintenance.Select(s => s.Text));
        Assert.All(db.Sink.Maintenance, s =>
        {
            Assert.Equal(nameof(MaintenanceOperation.Vacuum), s.Operation);
            Assert.Null(s.TransactionId);
            Assert.Null(s.Error);
            Assert.DoesNotContain("Host=", s.Text, StringComparison.OrdinalIgnoreCase);
        });
    }
}

// AC-134-02 (PG lock guard) and the Busy mapping of 55P03.
public sealed class PostgresVacuumContentionTests
{
    [PostgresFact(DisplayName = "AC-134-04 a table locked ACCESS EXCLUSIVE by another session makes FULL Busy within the timeout plus 2 s; the guard reports no event; the blocker commits normally")]
    public async Task A_locked_table_is_busy()
    {
        await using var db = await PostgresMaintenanceDatabase.CreateAsync();
        await PostgresVacuumFixture.SeedAsync(db);
        await using var blocker = await db.RawAsync();
        await using var locked = await blocker.BeginTransactionAsync();
        await SqliteMaintenanceDatabase.Exec(blocker, "LOCK TABLE vac_notes IN ACCESS EXCLUSIVE MODE", locked);
        db.Sink.Clear();

        var clock = Stopwatch.StartNew();
        var result = await db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Full) { Table = Descriptors.Table("vac_notes"), Timeout = TimeSpan.FromSeconds(1) });
        clock.Stop();

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1) + SqliteMaintenanceDatabase.Margin, "took " + clock.Elapsed);
        var statement = Assert.Single(db.Sink.Maintenance);
        Assert.Equal("VACUUM (FULL) \"vac_notes\"", statement.Text);
        Assert.DoesNotContain(db.Sink.Maintenance, s => s.Text.Contains("lock_timeout", StringComparison.Ordinal));
        await locked.CommitAsync();
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Full) { Table = Descriptors.Table("vac_notes") })).Status);
    }
}

// AC-134-11 on PostgreSQL: the holder refusal applies to Plain and FULL.
public sealed class PostgresVacuumHolderTests
{
    [PostgresFact(DisplayName = "AC-134-11 inside an open unit of work on the same database Plain and FULL are refused before SQL; after the commit they run")]
    public async Task The_holding_flow_is_refused()
    {
        await using var db = await PostgresMaintenanceDatabase.CreateAsync();
        await PostgresVacuumFixture.SeedAsync(db);
        var unit = await UnitOfWork.BeginAsync(db.Context);
        db.Sink.Clear();

        var plain = await Assert.ThrowsAsync<InvalidOperationException>(() => db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain)));
        var full = await Assert.ThrowsAsync<InvalidOperationException>(() => db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Full)));

        Assert.Contains("UnitOfWork", plain.Message, StringComparison.Ordinal);
        Assert.Contains("UnitOfWork", full.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", plain.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.Sink.Maintenance);
        await unit.CommitAsync();
        await unit.DisposeAsync();
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain))).Status);
    }
}

// AC-134-14 on PostgreSQL: cancelling a running FULL.
public sealed class PostgresVacuumCancellationTests
{
    [PostgresFact(DisplayName = "AC-134-14 cancelling a FULL VACUUM that waits for its lock cancels the command; the next call on the same pool succeeds")]
    public async Task Cancelling_full_vacuum()
    {
        await using var db = await PostgresMaintenanceDatabase.CreateAsync();
        await PostgresVacuumFixture.SeedAsync(db);
        var table = Descriptors.Table("vac_notes");
        await using (var blocker = await db.RawAsync())
        {
            await using var locked = await blocker.BeginTransactionAsync();
            await SqliteMaintenanceDatabase.Exec(blocker, "LOCK TABLE vac_notes IN ACCESS EXCLUSIVE MODE", locked);
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Full) { Table = table, Timeout = TimeSpan.FromSeconds(30) }, cancel.Token));
            await locked.CommitAsync();
        }

        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Full) { Table = table })).Status);
    }
}
