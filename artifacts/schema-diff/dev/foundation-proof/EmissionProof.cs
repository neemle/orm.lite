using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;

namespace FoundationProof;

internal static class EmissionProof
{
    internal static async Task RunAsync()
    {
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
            ?? throw new IOException("owned fixture required");
        await using var source = NpgsqlDataSource.Create(server);
        await using var postgres = await source.OpenConnectionAsync();
        await ExecuteAsync(postgres, null, "DROP TABLE IF EXISTS \"emit quoted\"");
        var pg = await VerifyAsync(postgres, PostgresProvider.Instance, "postgres:schema-diff-v1");
        var path = Path.Combine(Path.GetTempPath(), "orml117-emit-" + Guid.NewGuid().ToString("N") + ".db");
        SqliteProviderFactory.Register();
        await using var context = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = path });
        await using var sqlite = await SchemaMetadataConnections.OpenAsync(context, default);
        var sq = await VerifyAsync(sqlite, context.Provider, "sqlite:schema-diff-v1");
        Check.That(pg.Version == sq.Version, "missing-table neutral version");
        Console.WriteLine("EMISSION_PROOF_PASS runtime=" + Environment.Version);
    }

    private static async Task<SchemaDiffArtifact> VerifyAsync(DbConnection connection,
        ISqlProvider provider, string profile)
    {
        var request = Request(profile);
        var before = await ObserveAsync(connection, request);
        var report = SchemaDiff.Compare(request.Target, before);
        var name = profile.StartsWith("postgres", StringComparison.Ordinal) ? "Postgres" : "Sqlite";
        var artifact = SchemaDiff.Emit(report, new("0210_create", "Generated117", name + "CreateFactory"));
        var create = (CreateTable)artifact.Recipe.Nodes.Up.Single();
        if (provider is PostgresProvider) await NameCollisionAsync(connection, provider, create);
        await RollbackConflictAsync(connection, provider, create);
        var rolledBack = await ObserveAsync(connection, request);
        Check.That(rolledBack.Digest == before.Digest, "strict duplicate conflict rolls back missing table");
        foreach (var statement in SchemaStrictDdl.Compile(create, provider))
            await ExecuteAsync(connection, null, statement.Text);
        var after = await ObserveAsync(connection, request);
        AssertPhysical(artifact, after);
        await File.WriteAllTextAsync("artifacts/schema-diff/dev/generated/" + name + "CreateFactory.cs",
            artifact.CSharpSource);
        Check.Refuses(() => SchemaDiff.Emit(SchemaDiff.Compare(request.Target, after),
            new("0210_create", "Generated117", "EmptyFactory")), SchemaDiffError.UnsupportedDifference);
        Console.WriteLine("EMIT_PROVIDER_PASS " + name + " version=" + artifact.Version);
        return artifact;
    }

    private static void AssertPhysical(SchemaDiffArtifact artifact, SchemaDiffObservation after)
    {
        if (artifact.Recipe.After.Digest != after.Digest)
        {
            Console.WriteLine("EXPECTED " + System.Text.Json.JsonSerializer.Serialize(artifact.Recipe.After.Tables));
            Console.WriteLine("ACTUAL " + System.Text.Json.JsonSerializer.Serialize(after.Tables));
            foreach (var stamp in after.Tables.Single().Facts.DependentObjects.GetKnown())
                Console.WriteLine("ACTUAL_DEPENDENT " + stamp.Kind + " " + stamp.Name + " " + stamp.DefinitionDigest);
        }
        Check.That(artifact.Recipe.After.Digest == after.Digest, "pure projected After equals actual native metadata");
    }

    private static async Task RollbackConflictAsync(DbConnection connection, ISqlProvider provider, CreateTable create)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        var failed = false;
        foreach (var statement in SchemaStrictDdl.Compile(create, provider))
        {
            await ExecuteAsync(connection, transaction, statement.Text);
            try { await ExecuteAsync(connection, transaction, statement.Text); }
            catch (DbException) { failed = true; }
        }
        Check.That(failed, "actual strict duplicate conflict");
        await transaction.RollbackAsync();
    }

    private static async Task NameCollisionAsync(DbConnection connection, ISqlProvider provider, CreateTable create)
    {
        var column = create.Columns.OfType<ColumnDef>().Single(c => c.PrimaryKey).Column;
        var name = PostgresSerializer.StrictName(create.Table, column, "pk");
        await ExecuteAsync(connection, null, "DROP TABLE IF EXISTS \"collision owner\"");
        await ExecuteAsync(connection, null, "CREATE TABLE \"collision owner\" (id text CONSTRAINT \""
            + name + "\" PRIMARY KEY)");
        await ExecuteAsync(connection, null, "INSERT INTO \"collision owner\" VALUES ('owned-fixture-witness')");
        await using var transaction = await connection.BeginTransactionAsync();
        var failed = false;
        try
        {
            foreach (var statement in SchemaStrictDdl.Compile(create, provider))
                await ExecuteAsync(connection, transaction, statement.Text);
        }
        catch (DbException) { failed = true; }
        await transaction.RollbackAsync();
        Check.That(failed, "explicit strict PK name collision refuses without automatic suffix");
        await CollisionWitnessAsync(connection);
        await ExecuteAsync(connection, null, "DROP TABLE \"collision owner\"");
    }

    private static async Task CollisionWitnessAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM \"collision owner\"";
        Check.That((string?)await command.ExecuteScalarAsync() == "owned-fixture-witness",
            "conflicting owned fixture relation/data preserved");
        Console.WriteLine("STRICT_NAME_COLLISION_ROLLBACK_PASS");
    }

    private static SchemaDiffInspection Request(string profile)
    {
        var entity = EntityDescriptor.Of("EmitProof", "emit quoted", true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Title", "title", ColumnType.Text, true, false)
            .Property("Required", "required", ColumnType.Int32, false, false);
        var target = new SchemaDiffTarget(OrmSchema.Of("emit", entity), "model-v1",
            new("fixture117", profile.StartsWith("postgres", StringComparison.Ordinal) ? "public" : "main",
                ["emit quoted"]), new(2, 10, 100, 10, 100));
        return new(target, profile);
    }

    private static Task<SchemaDiffObservation> ObserveAsync(DbConnection connection, SchemaDiffInspection request) =>
        request.ExpectedProviderProfile.StartsWith("postgres", StringComparison.Ordinal)
            ? PostgresSchemaInspect.InspectAsync(connection, request, default)
            : SqliteSchemaInspect.InspectAsync(connection, request, default);

    private static async Task ExecuteAsync(DbConnection connection, DbTransaction? transaction, string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
