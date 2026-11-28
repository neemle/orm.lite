// §803 provenance: item ORML-138, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
// HELD (§803.8): admitted FAILS, routed by NINE-FIVE-ROUTING 7cd5d189 on ORML-138 to fix list item 1, the REINDEX lookup literal
// (TL fix list 6b33e96e on ORML-137). The Skip below is the HELD marker; the commit that turns it green removes it.
using System.Data.Common;
using System.Globalization;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class ReindexLookupLiteralR2AttackTests
{
    [PostgresFact(Skip = "HELD: routing 7cd5d189 on ORML-138 and ORML-137 (fix list 6b33e96e item 1)", DisplayName =
        "R2-F3 803 FULL round 2: lookup preserves backslashes with standard_conforming_strings off")]
    public async Task Lookup_does_not_rebuild_a_different_tables_index()
    {
        await using var db = await PostgresMaintenanceDatabase.CreateAsync();
        const string intendedName = @"line\nname";
        const string unrelatedName = "line\nname";
        const string indexName = "r2_shared_ix";

        var intendedSql =
            Descriptors.Quoted(PostgresProvider.Instance, intendedName);
        var unrelatedSql =
            Descriptors.Quoted(PostgresProvider.Instance, unrelatedName);

        await db.ExecAsync("CREATE SCHEMA target");
        await db.ExecAsync(
            "CREATE TABLE target." + intendedSql + " (id integer)");
        await db.ExecAsync(
            "CREATE TABLE public." + unrelatedSql + " (id integer)");
        await db.ExecAsync(
            "CREATE INDEX r2_shared_ix ON target." + intendedSql + " (id)");
        await db.ExecAsync(
            "CREATE INDEX r2_shared_ix ON public." + unrelatedSql + " (id)");

        var table = Descriptors.Table(intendedName)
            .Index(indexName, false, false, "id");

        var intendedBefore = await NodeAsync(db, "target");
        var unrelatedBefore = await NodeAsync(db, "public");

        await using var source =
            new ConfiguredSource(db.Context.DataSource);
        await using var context =
            new OrmContext(source, PostgresProvider.Instance);
        db.Sink.Clear();

        var result = await context.Maintenance.ReindexAsync(
            new ReindexRequest(ReindexScope.Index, ReindexMode.Blocking)
            {
                Table = table,
                Index = table.Indexes[0],
                Timeout = TimeSpan.FromSeconds(10)
            });

        var intendedAfter = await NodeAsync(db, "target");
        var unrelatedAfter = await NodeAsync(db, "public");

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.Equal(
            MaintenanceOperation.Reindex,
            Assert.Single(db.Sink.Maintenance).Operation);
        Assert.True(
            intendedBefore != intendedAfter &&
            unrelatedBefore == unrelatedAfter,
            $"Intended: {intendedBefore}->{intendedAfter}; " +
            $"unrelated: {unrelatedBefore}->{unrelatedAfter}");
    }

    private static async Task<long> NodeAsync(
        PostgresMaintenanceDatabase db, string schema) =>
        Convert.ToInt64(
            await db.ScalarAsync(
                "SELECT c.relfilenode FROM pg_class c " +
                "JOIN pg_namespace n ON n.oid = c.relnamespace " +
                "WHERE c.relname = 'r2_shared_ix' AND n.nspname = '" +
                schema + "'"),
            CultureInfo.InvariantCulture);

    private sealed class ConfiguredSource(DbDataSource inner) : DbDataSource
    {
        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            throw new NotSupportedException();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            var connection =
                await inner.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SET search_path TO public, target; " +
                    "SET standard_conforming_strings = off";
                await command.ExecuteNonQueryAsync(cancellationToken);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }
}
