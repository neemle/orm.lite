// §803 provenance: item ORML-136, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
using System.Globalization;
using Neemle.OrmLite.Tests.Nodes;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class ReindexLookupQuotingR2AttackTests
{
    [PostgresFact(DisplayName =
        "R2-F1 803 FULL round 2: Blocking lookup preserves backslash identifiers")]
    public Task Blocking() => CheckAsync(ReindexMode.Blocking);

    [PostgresFact(DisplayName =
        "R2-F1 803 FULL round 2: Concurrent lookup preserves backslash identifiers")]
    public Task Concurrent() => CheckAsync(ReindexMode.Concurrent);

    private static async Task CheckAsync(ReindexMode mode)
    {
        await using var db = await PostgresReindexFixture.OpenAsync();

        const string intendedTable = @"a\n";
        const string unrelatedTable = "a\n";

        await db.ExecAsync("CREATE SCHEMA tenant");
        await db.ExecAsync("ALTER TABLE public.a SET SCHEMA tenant");
        await db.ExecAsync(
            "ALTER TABLE tenant.a RENAME TO " + Quote(intendedTable));
        await db.ExecAsync(
            "ALTER TABLE public.b RENAME TO " + Quote(unrelatedTable));
        await db.ExecAsync(
            "CREATE INDEX ix_a_u ON public." + Quote(unrelatedTable) + " (k)");

        var builder = new NpgsqlConnectionStringBuilder(db.ConnectionString)
        {
            SearchPath = "public,tenant",
        };
        builder.Options =
            (builder.Options + " -c standard_conforming_strings=off").Trim();

        await using var context = await OrmLite.OpenAsync(new OrmLiteOptions
        {
            Provider = "postgres",
            ConnectionString = builder.ConnectionString,
        });

        var table = EntityDescriptor.Of("BackslashTable", intendedTable, true)
            .Index("ix_a_u", true, false, "u");
        var intendedBefore = await NodeAsync(db, "tenant.ix_a_u");
        var unrelatedBefore = await NodeAsync(db, "public.ix_a_u");

        var result = await context.Maintenance.ReindexAsync(
            new ReindexRequest(ReindexScope.Index, mode)
            {
                Table = table,
                Index = table.Indexes[0],
                Timeout = TimeSpan.FromSeconds(30),
            });

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.Equal(unrelatedBefore, await NodeAsync(db, "public.ix_a_u"));
        Assert.NotEqual(intendedBefore, await NodeAsync(db, "tenant.ix_a_u"));
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";

    // Both callers supply fixed, ordinary schema/index names.
    private static async Task<long> NodeAsync(
        PostgresMaintenanceDatabase db, string relation) =>
        Convert.ToInt64(
            await db.ScalarAsync(
                "SELECT pg_relation_filenode('" + relation + "'::regclass)"),
            CultureInfo.InvariantCulture);
}
