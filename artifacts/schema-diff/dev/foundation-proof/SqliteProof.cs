using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sqlite;
using System.Data.Common;

namespace FoundationProof;

internal static class SqliteProof
{
    internal static async Task RunAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "orml117-proof-" + Guid.NewGuid().ToString("N") + ".db");
        SqliteProviderFactory.Register();
        await using var context = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = path });
        await using var connection = await SchemaMetadataConnections.OpenAsync(context, default);
        await ExecuteAsync(connection, "CREATE TABLE \"proof quoted\" (id TEXT PRIMARY KEY NOT NULL, title TEXT NULL)");
        var request = Request();
        await ExistingAsync(connection, request);
        await MissingAsync(connection, request);
        await ExecuteAsync(connection, "DROP TABLE referrer");
        await MigrationRunner.RunAsync(context.DataSource, context.Provider, request.Target.Schema.Contributions());
        var next = new SchemaDiffInspection(TargetProof.Next(request.Target), request.ExpectedProviderProfile);
        var observed = await SqliteSchemaInspect.InspectAsync(connection, next, default);
        TargetProof.AddPlan(next.Target, observed);
        var again = await SqliteSchemaInspect.InspectAsync(connection, next, default);
        Check.That(observed.Digest == again.Digest && observed.HistoryDigest == again.HistoryDigest,
            "SQLite compare performs zero physical/history mutations");
        await HistoryAliasAsync(connection, next);
        Console.WriteLine("SQLITE_METADATA_COMPARE_PROOF_PASS runtime=" + connection.ServerVersion);
    }

    private static async Task ExistingAsync(DbConnection connection, SchemaDiffInspection request)
    {
        var first = await SqliteSchemaInspect.InspectAsync(connection, request, default);
        Check.That(SchemaDiff.Compare(request.Target, first).Status == SchemaDiffStatus.NoOp, "actual complete no-op");
        var second = await SqliteSchemaInspect.InspectAsync(connection, request, default);
        Check.That(first.Identity.Consistent, "coherent tracked metadata snapshot");
        Check.That(first.Digest == second.Digest, "SQLite stable physical inventory");
        Check.That(first.Tables.Single().Columns.Count == 2, "SQLite actual columns");
        Check.That(first.Tables.Single().Columns.Single(c => c.Name == "title").Facts.Default.GetKnown().Kind
            == SchemaDefaultKind.None, "SQLite actual absent default");
        Check.That(!first.History.HistoryTableExists.GetKnown(), "SQLite no history bootstrap");
    }

    private static async Task MissingAsync(DbConnection connection, SchemaDiffInspection request)
    {
        await ExecuteAsync(connection, "DROP TABLE \"proof quoted\"");
        var plainMissing = await SqliteSchemaInspect.InspectAsync(connection, request, default);
        Check.That(SchemaDiff.Compare(request.Target, plainMissing).Status == SchemaDiffStatus.Changes,
            "SQLite supported missing-table diff");
        await ExecuteAsync(connection, "CREATE TABLE referrer (parent TEXT REFERENCES \"proof quoted\"(id))");
        var missing = await SqliteSchemaInspect.InspectAsync(connection, request, default);
        Check.That(!missing.Tables.Single().Exists.GetKnown(), "SQLite actual missing table");
        Check.That(SchemaDiff.Compare(request.Target, missing).Status == SchemaDiffStatus.ManualRequired,
            "SQLite incoming effect blocks whole artifact");
        Check.That(missing.Tables.Single().Facts.DependentObjects.GetKnown().Count == 1,
            "SQLite incoming reference to nonexistent target is not known empty");
    }

    private static async Task HistoryAliasAsync(DbConnection connection, SchemaDiffInspection request)
    {
        await ExecuteAsync(connection, "ALTER TABLE main.schema_migrations RENAME TO history_temporary");
        await ExecuteAsync(connection, "ALTER TABLE main.history_temporary RENAME TO Schema_Migrations");
        try { await SqliteSchemaInspect.InspectAsync(connection, request, default); }
        catch (SchemaDiffException error) when (error.Error == SchemaDiffError.Ownership) { return; }
        throw new IOException("case-alias history must not be reported absent");
    }

    private static SchemaDiffInspection Request()
    {
        var entity = EntityDescriptor.Of("Proof", "proof quoted", true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Title", "title", ColumnType.Text, true, false);
        var target = new SchemaDiffTarget(OrmSchema.Of("proof", entity), "model-v1",
            new("isolated117", "main", ["proof quoted"]), new(2, 10, 100, 10, 50));
        return new SchemaDiffInspection(target, "sqlite:schema-diff-v1");
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
