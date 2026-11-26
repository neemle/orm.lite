using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;

namespace FoundationProof;

internal static class StrictProof
{
    internal static async Task RunAsync()
    {
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
            ?? throw new IOException("owned fixture required");
        await using var source = NpgsqlDataSource.Create(server);
        await using var postgres = await source.OpenConnectionAsync();
        await VerifyAsync(postgres, PostgresProvider.Instance);
        var path = Path.Combine(Path.GetTempPath(), "orml117-strict-" + Guid.NewGuid().ToString("N") + ".db");
        SqliteProviderFactory.Register();
        await using var context = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = path });
        await using var sqlite = await SchemaMetadataConnections.OpenAsync(context, default);
        await VerifyAsync(sqlite, context.Provider);
        Console.WriteLine("STRICT_CREATE_PROOF_PASS runtime=" + Environment.Version);
    }

    private static async Task VerifyAsync(DbConnection connection, ISqlProvider provider)
    {
        var node = new CreateTable("strict \"quoted", NodeList.Of(new[]
        { new ColumnDef("id", new TypeRef(ColumnType.Int32), false, true) }));
        var strict = SchemaStrictDdl.Compile(node, provider).Single().Text;
        var ordinary = DdlCompiler.Compile(node, provider).Single().Text;
        Check.That(!strict.Contains("IF NOT EXISTS", StringComparison.OrdinalIgnoreCase), "strict syntax");
        Check.That(ordinary.Contains("IF NOT EXISTS", StringComparison.OrdinalIgnoreCase), "ordinary syntax retained");
        await ExecuteAsync(connection, strict);
        await ExecuteAsync(connection, ordinary);
        var failed = false;
        try { await ExecuteAsync(connection, strict); }
        catch (DbException) { failed = true; }
        Check.That(failed, "actual duplicate strict-create refuses");
        await ExecuteAsync(connection, DdlCompiler.Compile(new DropTable(node.Table), provider).Single().Text);
        Console.WriteLine("STRICT_CREATE_PROVIDER_PASS " + provider.GetType().Name);
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
