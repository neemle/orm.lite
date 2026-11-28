// §803 provenance: item ORML-136, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using System.Data.Common;
using System.Globalization;
using Neemle.OrmLite.Tests.Nodes;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class ReindexSearchPathAttackTests
{
    [PostgresFact(DisplayName =
        "F1 803 FULL round 1: Blocking rebuilds the index belonging to the resolved table")]
    public Task Blocking_targets_the_right_index() =>
        CheckAsync(ReindexMode.Blocking, dropTarget: false);

    [PostgresFact(DisplayName =
        "F1 803 FULL round 1: Concurrent rebuilds the index belonging to the resolved table")]
    public Task Concurrent_targets_the_right_index() =>
        CheckAsync(ReindexMode.Concurrent, dropTarget: false);

    [PostgresFact(DisplayName =
        "F1 803 FULL round 1: Blocking does not substitute another index for a missing target")]
    public Task Blocking_rejects_a_missing_target() =>
        CheckAsync(ReindexMode.Blocking, dropTarget: true);

    [PostgresFact(DisplayName =
        "F1 803 FULL round 1: Concurrent does not substitute another index for a missing target")]
    public Task Concurrent_rejects_a_missing_target() =>
        CheckAsync(ReindexMode.Concurrent, dropTarget: true);

    private static async Task CheckAsync(ReindexMode mode, bool dropTarget)
    {
        await using var db = await PostgresReindexFixture.OpenAsync();

        // Moving the table also moves its indexes.
        await db.ExecAsync("CREATE SCHEMA tenant");
        await db.ExecAsync("ALTER TABLE public.a SET SCHEMA tenant");
        await db.ExecAsync("CREATE INDEX ix_a_u ON public.b (k)");

        var connectionString = new NpgsqlConnectionStringBuilder(db.ConnectionString)
        {
            SearchPath = "public,tenant",
        }.ConnectionString;

        await using var context = await OrmLite.OpenAsync(new OrmLiteOptions
        {
            Provider = "postgres",
            ConnectionString = connectionString,
        });

        var targetBefore = await NodeAsync(db, "tenant.ix_a_u");
        var unrelatedBefore = await NodeAsync(db, "public.ix_a_u");

        var request = new ReindexRequest(ReindexScope.Index, mode)
        {
            Table = ReindexFixture.A,
            Index = ReindexFixture.Unique,
            Timeout = TimeSpan.FromSeconds(30),
        };

        if (dropTarget)
        {
            await db.ExecAsync("DROP INDEX tenant.ix_a_u");

            await Assert.ThrowsAnyAsync<DbException>(
                () => context.Maintenance.ReindexAsync(request));

            Assert.Equal(unrelatedBefore, await NodeAsync(db, "public.ix_a_u"));
            return;
        }

        var result = await context.Maintenance.ReindexAsync(request);
        var targetAfter = await NodeAsync(db, "tenant.ix_a_u");
        var unrelatedAfter = await NodeAsync(db, "public.ix_a_u");

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.True(
            targetBefore != targetAfter && unrelatedBefore == unrelatedAfter,
            $"{mode}: intended index {targetBefore}->{targetAfter}; " +
            $"unrelated index {unrelatedBefore}->{unrelatedAfter}");
    }

    private static async Task<long> NodeAsync(
        PostgresMaintenanceDatabase db, string qualifiedName) =>
        Convert.ToInt64(
            await db.ScalarAsync(
                "SELECT pg_relation_filenode('" + qualifiedName + "'::regclass)"),
            CultureInfo.InvariantCulture);
}
