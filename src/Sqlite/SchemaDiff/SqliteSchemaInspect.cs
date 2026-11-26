using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Neemle.OrmLite.Sqlite;

internal static class SqliteSchemaInspect
{
    internal static async Task<SchemaDiffObservation> InspectAsync(DbConnection connection,
        SchemaDiffInspection request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (connection is not SqliteConnection native)
            throw new SchemaDiffException(SchemaDiffError.UnsupportedProvider);
        var resources = new SqliteMetadataResources();
        var tx = native.BeginTransaction(deferred: true);
        resources.Add(tx);
        SchemaDiffObservation? observation = null;
        Exception? primary = null;
        try { observation = await CoreAsync(new SqliteSchemaRead(connection, tx, request), ct); }
        catch (Exception error) { primary = error; }
        await resources.FinishAsync(primary, ct);
        return observation!;
    }

    private static async Task<SchemaDiffObservation> CoreAsync(SqliteSchemaRead read, CancellationToken ct)
    {
        var databases = await SqliteSchemaAdmission.ValidateAsync(read, ct);
        var schema = await read.RowsAsync("SELECT type,name,tbl_name,sql FROM main.sqlite_schema", [],
            read.Request.Target.Limits.MaxCatalogObjects, ct);
        foreach (var _ in schema) read.Object();
        var tables = new List<SchemaObservedTable>();
        foreach (var table in read.Request.Target.Scope.OwnedTables)
            tables.Add(await SqliteSchemaTable.ReadAsync(read, table, schema, ct));
        var history = await HistoryAsync(read, schema, ct);
        var mainFile = databases.Single(d => (string)d[0]! == "main")[1];
        var identity = SqliteSchemaRead.Hash(read.Connection.DataSource + "|" + mainFile);
        return SchemaDiffObservation.FromFacts(new(identity, "sqlite:schema-diff-v1", true),
            tables, history, read.Request);
    }

    private static async Task<SchemaObservationHistory> HistoryAsync(SqliteSchemaRead read,
        IReadOnlyList<object?[]> schema, CancellationToken ct)
    {
        var history = schema.SingleOrDefault(r => (string)r[1]! == "schema_migrations");
        if (history is null) return new(SchemaFact<bool>.Known(false), SchemaFact<IReadOnlyList<string>>.Known([]));
        if ((string)history[0]! != "table") return new(SchemaFact<bool>.Unsupported("history-kind"),
            SchemaFact<IReadOnlyList<string>>.Unknown("history-kind"));
        var versions = await read.RowsAsync("SELECT version FROM main.schema_migrations", [],
            read.Request.Target.Limits.MaxCatalogObjects, ct);
        return new(SchemaFact<bool>.Known(true),
            SchemaFact<IReadOnlyList<string>>.Known(versions.Select(r => (string)r[0]!).ToArray()));
    }
}
