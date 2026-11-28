// §803 provenance: item ORML-135, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
// HELD (§803.8): admitted FAILS, routed by the TL fix list a3a56082 on ORML-137 to follow-up ORML-155 (a target-existence
// probe before SQLite ANALYZE). The Skip below is the HELD marker; it is removed when ORML-155 lands.
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class AnalyzeIndexCollisionAttackTests
{
    [Fact(Skip = "HELD: ORML-155 (TL fix list a3a56082 on ORML-137) owes the fix", DisplayName =
        "F1 — 803 FULL round 1 — missing table must not resolve to another table's index")]
    public async Task Missing_table_with_matching_index_throws_native_error()
    {
        await using var db = await AnalyzeFixture.OpenAsync();

        // Keep the descriptor for a, but replace that database object
        // with an index of the same name belonging to b.
        await db.ExecAsync("DROP TABLE a");
        await db.ExecAsync("CREATE INDEX a ON b(k)");

        var failure = await Assert.ThrowsAsync<SqliteException>(
            () => db.Maintenance.AnalyzeAsync(
                AnalyzeFixture.Table(AnalyzeFixture.A)));

        Assert.Equal(1, failure.SqliteErrorCode);
        Assert.Contains(
            "no such table",
            failure.Message,
            StringComparison.OrdinalIgnoreCase);
    }
}
