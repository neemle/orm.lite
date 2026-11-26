using Neemle.OrmLite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;

namespace FoundationProof;

internal static class BridgeProof
{
    internal static async Task RunAsync()
    {
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
            ?? throw new IOException("owned fixture required");
        await using var source = NpgsqlDataSource.Create(server);
        await using var postgres = new OrmContext(source, PostgresProvider.Instance);
        await BridgeSql.ExecuteAsync(source, "DROP TABLE IF EXISTS " + BridgeSql.Quoted("bridge items"));
        await BridgeSql.ExecuteAsync(source, "DROP TABLE IF EXISTS " + BridgeSql.Quoted("bridge created"));
        await BridgeSql.ExecuteAsync(source, "DROP TABLE IF EXISTS schema_migrations");
        foreach (var create in new[] { false, true }) await PostgresAsync(postgres, server, create);
        SqliteProviderFactory.Register();
        foreach (var create in new[] { false, true }) await SqliteAsync(create);
        Console.WriteLine("BRIDGE_PROOF_PASS runtime=" + Environment.Version);
    }

    private static async Task PostgresAsync(OrmContext context, string server, bool create)
    {
        var shape = BridgeCase.Of("public", create);
        var artifact = await PrepareAsync(context, shape);
        await BridgeActions.UpgradeAsync(context, shape, artifact);
        var database = "orml117_fresh_" + Guid.NewGuid().ToString("N")[..12];
        await BridgeSql.ExecuteAsync(context.DataSource, "CREATE DATABASE " + database);
        var builder = new NpgsqlConnectionStringBuilder(server) { Database = database };
        await using var source = NpgsqlDataSource.Create(builder.ConnectionString);
        await using var fresh = new OrmContext(source, PostgresProvider.Instance);
        await BridgeActions.FreshAsync(fresh, shape, artifact);
    }

    private static async Task SqliteAsync(bool create)
    {
        var folder = "artifacts/schema-diff/dev/fixtures";
        Directory.CreateDirectory(folder);
        var path = Path.GetFullPath(Path.Combine(folder, "bridge-" + Guid.NewGuid().ToString("N") + ".db"));
        await using var context = await OrmLite.OpenAsync(new OrmLiteOptions
            { Provider = "sqlite", Path = path, Pooling = false });
        var shape = BridgeCase.Of("main", create);
        var artifact = await PrepareAsync(context, shape);
        await BridgeActions.UpgradeAsync(context, shape, artifact);
        await using var fresh = await OrmLite.OpenAsync(new OrmLiteOptions
            { Provider = "sqlite", Path = path + ".fresh", Pooling = false });
        await BridgeActions.FreshAsync(fresh, shape, artifact);
    }

    private static async Task<SchemaDiffArtifact> PrepareAsync(OrmContext context, BridgeCase shape)
    {
        await MigrationRunner.RunAsync(context.DataSource, context.Provider, shape.Before.Contributions());
        if (!shape.Created) await BridgeSql.ExecuteAsync(context.DataSource,
            "INSERT INTO " + BridgeSql.Quoted("bridge items")
                + " (id,title) VALUES ('existing-id','original-private-title')");
        var before = await SchemaDiff.ObserveAsync(context, shape.Target);
        var report = SchemaDiff.Compare(shape.Target, before);
        Check.That(report.Status == SchemaDiffStatus.Changes, "complete supported bridge report");
        return SchemaDiff.Emit(report, new(shape.Prefix, "Generated117", "BridgeFactory"));
    }
}
