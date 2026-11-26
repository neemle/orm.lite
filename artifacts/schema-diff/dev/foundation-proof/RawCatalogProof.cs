using System.Text.Json;
using Npgsql;

namespace FoundationProof;

internal static class RawCatalogProof
{
    private const string Sql = """
        SELECT 'constraint:'||c.contype::text,c.conname::text,pg_catalog.pg_get_constraintdef(c.oid,true)
          FROM pg_catalog.pg_constraint c WHERE c.conrelid=@table::regclass
        UNION ALL SELECT 'index',i.relname::text,pg_catalog.pg_get_indexdef(i.oid)
          FROM pg_catalog.pg_index x JOIN pg_catalog.pg_class i ON i.oid=x.indexrelid
          WHERE x.indrelid=@table::regclass
        UNION ALL SELECT d.classid::regclass::text||':'||d.deptype::text,
          o.type||':'||o.identity,o.identity||':'||d.refobjsubid::text
          FROM pg_catalog.pg_depend d CROSS JOIN LATERAL
            pg_catalog.pg_identify_object(d.classid,d.objid,d.objsubid) o
          WHERE d.refclassid='pg_catalog.pg_class'::regclass AND d.refobjid=@table::regclass
        """;

    internal static async Task RunAsync()
    {
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
            ?? throw new IOException("owned fixture required");
        await using var source = NpgsqlDataSource.Create(server);
        await using var connection = await source.OpenConnectionAsync();
        await DumpAsync(connection, "\"recipe quoted\"");
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE \"no toast\" (id integer PRIMARY KEY);"
            + "CREATE TABLE \"default sql\" (id integer PRIMARY KEY, title text NOT NULL DEFAULT '')";
        await command.ExecuteNonQueryAsync();
        await DumpAsync(connection, "\"no toast\"");
        await DumpAsync(connection, "\"default sql\"");
    }

    private static async Task DumpAsync(NpgsqlConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Sql;
        command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync())
        {
            Check.That(++count <= 100, "bounded native metadata proof");
            Console.WriteLine(JsonSerializer.Serialize(new[] { table,
                reader.GetString(0), reader.GetString(1), reader.GetString(2) }));
        }
    }
}
