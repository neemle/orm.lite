using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;

namespace FoundationProof;

internal static class RecipeProof
{
    internal static async Task RunAsync()
    {
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
            ?? throw new IOException("owned fixture required");
        await using var source = NpgsqlDataSource.Create(server);
        await using var postgres = await source.OpenConnectionAsync();
        await ExecuteAsync(postgres, "DROP TABLE IF EXISTS \"recipe quoted\"");
        await ExecuteAsync(postgres, "DROP TABLE IF EXISTS schema_migrations");
        var pg = await VerifyAsync(source, postgres, PostgresProvider.Instance, "postgres:schema-diff-v1");
        var path = Path.Combine(Path.GetTempPath(), "orml117-recipe-" + Guid.NewGuid().ToString("N") + ".db");
        SqliteProviderFactory.Register();
        await using var context = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = path });
        await using var sqlite = await SchemaMetadataConnections.OpenAsync(context, default);
        var sq = await VerifyAsync(context.DataSource, sqlite, context.Provider, "sqlite:schema-diff-v1");
        Check.That(pg.Version == sq.Version, "neutral provider-equivalent version");
        Check.That(pg.ReviewDigest != sq.ReviewDigest, "physical provider review separated");
        Console.WriteLine("RECIPE_PROOF_PASS runtime=" + Environment.Version);
    }

    private static async Task<SchemaDiffArtifact> VerifyAsync(DbDataSource source, DbConnection connection,
        ISqlProvider provider, string profile)
    {
        var entity = EntityDescriptor.Of("Proof", "recipe quoted", true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Title", "title", ColumnType.Text, true, false);
        var schema = OrmSchema.Of("recipe", entity);
        await MigrationRunner.RunAsync(source, provider, schema.Contributions());
        var target = TargetProof.Next(new(schema, "model-v1",
            new("fixture117", profile.StartsWith("postgres", StringComparison.Ordinal) ? "public" : "main",
                ["recipe quoted"]), new(2, 10, 100, 10, 100)));
        var request = new SchemaDiffInspection(target, profile);
        var before = await ObserveAsync(connection, request);
        var report = SchemaDiff.Compare(target, before);
        Check.That(report.Status == SchemaDiffStatus.Changes, "recipe additive report");
        foreach (var statement in report.Comparison.Up.SelectMany(node => DdlCompiler.Compile(node, provider)))
            await ExecuteAsync(connection, statement.Text);
        var after = await ObserveAsync(connection, request);
        var artifact = Artifact(report, after);
        Directory.CreateDirectory("artifacts/schema-diff/dev/generated");
        var name = profile.StartsWith("postgres", StringComparison.Ordinal) ? "Postgres" : "Sqlite";
        await File.WriteAllTextAsync("artifacts/schema-diff/dev/generated/" + name + "Factory.cs",
            artifact.CSharpSource);
        Console.WriteLine("RECIPE_PROVIDER_PASS " + name + " version=" + artifact.Version
            + " review=" + artifact.ReviewDigest);
        return artifact;
    }

    private static SchemaDiffArtifact Artifact(SchemaDiffReport report, SchemaDiffObservation after)
    {
        var before = report.Comparison.Before;
        var name = before.ProviderProfile.StartsWith("postgres", StringComparison.Ordinal) ? "Postgres" : "Sqlite";
        var manifest = new SchemaDiffManifest(new("0200_add", "Generated117", name + "Factory"), before.Target.Scope,
            new(before.Target.ModelVersion, SchemaRecipeIdentity.Target(before.Target), before.Digest,
                before.ProviderProfile), before.Target.SchemaToken);
        var up = report.Comparison.Up.ToList();
        var recipe = new SchemaDiffRecipe(manifest, new(up, SchemaNodeCopies.Down(up)), before, after);
        var artifact = SchemaDiffArtifact.FromRecipe(recipe);
        up.Clear();
        Check.That(artifact.Recipe.Nodes.Up.Count == 1 && artifact.DownProofRequired, "immutable copy/down obligation");
        Check.That(artifact.CoverageVersion != artifact.Version, "coverage receipt distinct");
        var hostile = recipe with { Nodes = new([new RawSql(null, "DELETE FROM fake_private")], []) };
        Check.Refuses(() => SchemaDiffArtifact.FromRecipe(hostile), SchemaDiffError.InvalidArtifact);
        return artifact;
    }

    private static Task<SchemaDiffObservation> ObserveAsync(DbConnection connection, SchemaDiffInspection request) =>
        request.ExpectedProviderProfile.StartsWith("postgres", StringComparison.Ordinal)
            ? PostgresSchemaInspect.InspectAsync(connection, request, default)
            : SqliteSchemaInspect.InspectAsync(connection, request, default);

    private static async Task ExecuteAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
