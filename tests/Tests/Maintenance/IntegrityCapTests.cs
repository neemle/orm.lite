using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// ADR e8c50bcc v5 D12.6 (ORML-138 F2): SQLite's table-scoped Full and Quick stop at 100 messages, and a check that
// reaches that cap is reported as Truncated.
public sealed class IntegrityCapTests
{
    [Theory(DisplayName = "ORML-138 D12.6 SQLite caps only the table-scoped Full and Quick checks, at 100")]
    [InlineData(IntegrityCheckMode.Full, true, 100)]
    [InlineData(IntegrityCheckMode.Quick, true, 100)]
    [InlineData(IntegrityCheckMode.ForeignKeys, true, null)]
    [InlineData(IntegrityCheckMode.Full, false, null)]
    [InlineData(IntegrityCheckMode.Quick, false, null)]
    [InlineData(IntegrityCheckMode.ForeignKeys, false, null)]
    public void The_engine_cap(IntegrityCheckMode mode, bool scoped, int? cap) =>
        Assert.Equal(cap, SqliteProvider.Instance.IntegrityEngineCap(mode, scoped ? Descriptors.Table("t") : null));

    [Fact(DisplayName = "ORML-138 D12.6 a table-scoped check below the cap is complete: not Truncated, and the result names its provider")]
    public async Task Below_the_cap_is_not_truncated()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        await IntegrityFixtures.CorruptIndexAsync(db, rows: 3);

        var result = await db.Maintenance.CheckIntegrityAsync(
            IntegrityFixtures.Request(IntegrityCheckMode.Full, Descriptors.Table("t"), maxFindings: 1000));

        Assert.Equal(MaintenanceStatus.IssuesFound, result.Status);
        Assert.InRange(result.Findings.Count, 1, 99);
        Assert.False(result.Truncated);
        Assert.Equal("sqlite", result.Provider);
        Assert.Equal(MaintenanceOperation.IntegrityCheck, result.Operation);
    }

    [Fact(DisplayName = "ORML-138 D12.6 the whole-database check is not capped: it reports past 100 up to MaxFindings and is Truncated beyond")]
    public async Task The_whole_database_check_is_not_capped()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        await IntegrityFixtures.CorruptIndexAsync(db, rows: 200);

        var wide = await db.Maintenance.CheckIntegrityAsync(IntegrityFixtures.Request(IntegrityCheckMode.Full, maxFindings: 150));

        Assert.Equal(150, wide.Findings.Count);
        Assert.True(wide.Truncated);
    }
}
