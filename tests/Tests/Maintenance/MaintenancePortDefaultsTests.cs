using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// The default interface members of IMaintenanceProvider, which a third-party port inherits, and the provider render
// arms that only a value outside the enum or a missing target reaches. The core refuses both before any render runs
// (undefined modes in validation, unsupported operations by capability), so they are reached here directly.
public sealed class MaintenancePortDefaultsTests
{
    private static readonly IMaintenanceProvider Minimal = new MinimalPort();

    [Fact(DisplayName = "a port that implements only the required members gets no gate, no contention, no guard, no retry slice, no lookup and no cap")]
    public void Optional_members_default_to_nothing()
    {
        Assert.False(Minimal.HasWriterGate);
        Assert.False(Minimal.IsContention(new SqliteException("busy", 5)));
        Assert.Null(Minimal.RenderLockGuard(TimeSpan.FromSeconds(1)));
        Assert.Null(Minimal.ContentionRetrySlice);
        Assert.Null(Minimal.RenderIndexSchemaLookup(Descriptors.Table("t")));
        Assert.Null(Minimal.IntegrityEngineCap(IntegrityCheckMode.Full, Descriptors.Table("t")));
    }

    [Fact(DisplayName = "a port that implements only the required members renders no operation: each render says so")]
    public void Renders_default_to_not_supported()
    {
        var table = Descriptors.Table("t");

        Assert.Throws<NotSupportedException>(() => Minimal.RenderWalCheckpoint(WalCheckpointMode.Passive));
        Assert.Throws<NotSupportedException>(() => Minimal.RenderVacuum(VacuumMode.Plain, null));
        Assert.Throws<NotSupportedException>(() => Minimal.RenderAnalyze(AnalyzeScope.Table, table));
        Assert.Throws<NotSupportedException>(() => Minimal.RenderReindex(ReindexScope.Table, ReindexMode.Blocking, table, null, null));
        Assert.Throws<NotSupportedException>(() => Minimal.RenderIntegrityCheck(IntegrityCheckMode.Full, null, 1));
        Assert.Throws<NotSupportedException>(() => Minimal.ReadIntegrityFinding(IntegrityCheckMode.Full, null!));
    }

    [Fact(DisplayName = "SQLite renders nothing for a value outside an enum or a scope without its target")]
    public void Sqlite_render_arms()
    {
        var sqlite = SqliteProvider.Instance;
        var table = ReindexFixture.A;

        Assert.Throws<ArgumentOutOfRangeException>(() => sqlite.RenderWalCheckpoint((WalCheckpointMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => sqlite.RenderVacuum(VacuumMode.Full, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => sqlite.RenderVacuum(VacuumMode.Plain, table));
        Assert.Throws<ArgumentOutOfRangeException>(() => sqlite.RenderAnalyze((AnalyzeScope)99, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => sqlite.RenderAnalyze(AnalyzeScope.Table, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => sqlite.RenderReindex((ReindexScope)99, ReindexMode.Blocking, null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => sqlite.RenderReindex(ReindexScope.Table, ReindexMode.Blocking, null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => sqlite.RenderReindex(ReindexScope.Index, ReindexMode.Blocking, table, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => sqlite.RenderReindex(ReindexScope.Database, (ReindexMode)99, null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => sqlite.RenderIntegrityCheck((IntegrityCheckMode)99, null, 1));
    }

    [Fact(DisplayName = "PostgreSQL renders nothing for a value outside an enum or a scope without its target")]
    public void Postgres_render_arms()
    {
        var postgres = PostgresProvider.Instance;
        var table = ReindexFixture.A;

        Assert.Throws<ArgumentOutOfRangeException>(() => postgres.RenderVacuum((VacuumMode)99, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => postgres.RenderAnalyze((AnalyzeScope)99, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => postgres.RenderAnalyze(AnalyzeScope.Table, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => postgres.RenderReindex((ReindexScope)99, ReindexMode.Blocking, null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => postgres.RenderReindex(ReindexScope.Table, ReindexMode.Blocking, null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => postgres.RenderReindex(ReindexScope.Index, ReindexMode.Blocking, table, null, "public"));
        Assert.Throws<ArgumentOutOfRangeException>(() => postgres.RenderReindex(ReindexScope.Database, (ReindexMode)99, null, null, null));
    }

    // The three members every port must implement, and nothing else.
    private sealed class MinimalPort : IMaintenanceProvider
    {
        public TimeSpan MaintenanceTimeout => TimeSpan.FromSeconds(1);

        public string? WriterKeyOf(DbDataSource source) => null;

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
            ValueTask.FromResult(new MaintenanceWriteLease(true, null));
    }
}
