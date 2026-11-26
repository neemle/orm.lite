using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaInspect
{
    internal static async Task<SchemaDiffObservation> InspectAsync(DbConnection connection,
        SchemaDiffInspection request, CancellationToken ct)
    {
        var tx = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var resources = new PostgresMetadataResources();
        resources.Add(tx);
        SchemaDiffObservation? observation = null;
        Exception? primary = null;
        try { observation = await CoreAsync(new PostgresSchemaRead(connection, tx, request), ct); }
        catch (Exception error) { primary = error; }
        await resources.FinishAsync(primary, ct);
        return observation!;
    }

    private static async Task<SchemaDiffObservation> CoreAsync(PostgresSchemaRead read, CancellationToken ct)
    {
        var connection = read.Connection;
        var request = read.Request;
        await read.RowsAsync("SET TRANSACTION READ ONLY", [], 1, ct);
        var identity = await read.RowsAsync("SELECT current_database(),current_schema(),"
            + "current_setting('server_version_num'),"
            + "(SELECT oid FROM pg_catalog.pg_database WHERE datname=current_database())",
            [], 1, ct);
        if (identity.Count == 1 && (!int.TryParse((string)identity[0][2]!, out var version)
            || version is < 190000 or >= 200000)) throw new SchemaDiffException(SchemaDiffError.UnsupportedProvider);
        if (identity.Count != 1 || (string)identity[0][1]! != request.Target.Scope.Namespace)
            throw new SchemaDiffException(SchemaDiffError.ScopeMismatch);
        var tables = new List<SchemaObservedTable>();
        foreach (var table in request.Target.Scope.OwnedTables)
            tables.Add(await PostgresSchemaTable.ReadAsync(read, table, ct));
        var history = await PostgresSchemaHistory.ReadAsync(read, ct);
        var database = PostgresSchemaRead.Fingerprint(new object?[] { connection.DataSource }.Concat(identity[0]));
        return SchemaDiffObservation.FromFacts(new(database, "postgres:schema-diff-v1", true),
            tables, history, request);
    }

}
