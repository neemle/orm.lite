using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaInspect
{
    internal static async Task<SchemaDiffObservation> InspectAsync(DbConnection connection,
        SchemaDiffInspection request, CancellationToken ct)
    {
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var read = new PostgresSchemaRead(connection, tx, request);
        await read.RowsAsync("SET TRANSACTION READ ONLY", [], 1, ct);
        var identity = await read.RowsAsync("SELECT current_database(),current_schema(),"
            + "current_setting('server_version_num'),(SELECT oid FROM pg_catalog.pg_database WHERE datname=current_database())",
            [], 1, ct);
        if (identity.Count != 1 || (string)identity[0][1]! != request.Target.Scope.Namespace)
            throw new InvalidOperationException("metadata namespace");
        var tables = new List<SchemaObservedTable>();
        foreach (var table in request.Target.Scope.OwnedTables)
            tables.Add(await PostgresSchemaTable.ReadAsync(read, table, ct));
        var history = await HistoryAsync(read, ct);
        var database = PostgresSchemaRead.Hash(connection.DataSource + "|" + string.Join("|", identity[0]));
        return SchemaDiffObservation.FromFacts(new(database, "postgres:schema-diff-v1", true), tables, history, request);
    }

    private static async Task<SchemaObservationHistory> HistoryAsync(PostgresSchemaRead read, CancellationToken ct)
    {
        var exists = await read.RowsAsync("SELECT EXISTS(SELECT 1 FROM pg_catalog.pg_class c "
            + "JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace "
            + "WHERE n.nspname=current_schema() AND c.relname='schema_migrations' AND c.relkind='r')", [], 1, ct);
        if (!(bool)exists[0][0]!) return new(SchemaFact<bool>.Known(false),
            SchemaFact<IReadOnlyList<string>>.Known([]));
        var ns = read.Request.Target.Scope.Namespace.Replace("\"", "\"\"", StringComparison.Ordinal);
        var versions = await read.RowsAsync("SELECT version FROM \"" + ns + "\".\"schema_migrations\"", [],
            read.Request.Target.Limits.MaxCatalogObjects, ct);
        return new(SchemaFact<bool>.Known(true),
            SchemaFact<IReadOnlyList<string>>.Known(versions.Select(r => (string)r[0]!).ToArray()));
    }
}
