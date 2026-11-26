using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Npgsql;

namespace FoundationProof;

internal static class PostgresProof
{
    internal static async Task RunAsync()
    {
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
            ?? throw new IOException("owned fixture required");
        await using var source = NpgsqlDataSource.Create(server);
        await using var connection = await source.OpenConnectionAsync();
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS \"proof quoted\"");
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS schema_migrations");
        await ExecuteAsync(connection, "CREATE TABLE \"proof quoted\" (id integer PRIMARY KEY, title text NULL)");
        await ExecuteAsync(connection, "INSERT INTO \"proof quoted\" VALUES (1,'private-unread-row')");
        var request = Request();
        await ExistingAsync(connection, request);
        await MissingAndAddAsync(source, connection, request);
        Console.WriteLine("POSTGRES_METADATA_COMPARE_PROOF_PASS");
    }

    private static async Task ExistingAsync(NpgsqlConnection connection, SchemaDiffInspection request)
    {
        var first = await PostgresSchemaInspect.InspectAsync(connection, request, default);
        Check.That(SchemaDiff.Compare(request.Target, first).Status == SchemaDiffStatus.NoOp, "actual complete no-op");
        var second = await PostgresSchemaInspect.InspectAsync(connection, request, default);
        Check.That(first.Digest == second.Digest, "stable physical inventory");
        Check.That(first.Tables.Single().Columns.Count == 2, "actual column census");
        Check.That(first.Tables.Single().Columns.Single(c => c.Name == "title").Facts.Default.GetKnown().Kind
            == SchemaDefaultKind.None, "observed absent default");
        Check.That(!first.History.HistoryTableExists.GetKnown(), "no history bootstrap");
        Check.That(first.Tables.Single().Facts.Indexes.GetKnown().Count == 1, "actual primary index");
        var constraints = first.Tables.Single().Facts.Constraints.GetKnown();
        Check.That(constraints.Count(c => c.Kind == "constraint:p") == 1, "actual primary constraint");
        Check.That(constraints.Count(c => c.Kind == "constraint:n") == 1, "actual PG19 not-null constraint");
        Check.That(first.Tables.Single().Facts.DependentObjects.GetKnown().Count > 0, "actual dependencies");
    }

    private static async Task MissingAndAddAsync(NpgsqlDataSource source,
        NpgsqlConnection connection, SchemaDiffInspection request)
    {
        await ExecuteAsync(connection, "DROP TABLE \"proof quoted\"");
        var missing = await PostgresSchemaInspect.InspectAsync(connection, request, default);
        Check.That(!missing.Tables.Single().Exists.GetKnown(), "actual missing table");
        Check.That(SchemaDiff.Compare(request.Target, missing).Status == SchemaDiffStatus.Changes,
            "actual supported missing-table diff");
        await MigrationRunner.RunAsync(source, PostgresProvider.Instance, request.Target.Schema.Contributions());
        var next = new SchemaDiffInspection(TargetProof.Next(request.Target), request.ExpectedProviderProfile);
        var observed = await PostgresSchemaInspect.InspectAsync(connection, next, default);
        TargetProof.AddPlan(next.Target, observed);
        var again = await PostgresSchemaInspect.InspectAsync(connection, next, default);
        Check.That(observed.Digest == again.Digest && observed.HistoryDigest == again.HistoryDigest,
            "compare performs zero physical/history mutations");
    }

    private static SchemaDiffInspection Request()
    {
        var entity = EntityDescriptor.Of("Proof", "proof quoted", true)
            .Property("Id", "id", ColumnType.Int32, false, true)
            .Property("Title", "title", ColumnType.Text, true, false);
        var target = new SchemaDiffTarget(OrmSchema.Of("proof", entity), "model-v1",
            new("isolated117", "public", ["proof quoted"]), new(2, 10, 100, 10, 50));
        return new SchemaDiffInspection(target, "postgres:schema-diff-v1");
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
