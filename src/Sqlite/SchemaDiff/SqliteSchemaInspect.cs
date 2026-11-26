using Data = global::Neemle.OrmLite.Sqlite.SqliteSchemaDataLiterals;
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
        var tx = native is OrmSqliteConnection mapped
            ? mapped.BeginMetadataTransaction() : native.BeginTransaction(deferred: true);
        resources.Add(tx);
        SchemaDiffObservation? observation = null;
        Exception? primary = null;
        try { observation = await CoreAsync(new SqliteSchemaRead(connection, tx, request), ct); }
        catch (Exception error) { primary = error; }
        await resources.FinishAsync(primary, ct);
        return observation!;
    }

    internal static Task<SchemaDiffObservation> GuardedAsync(DbConnection connection,
        DbTransaction transaction, SchemaDiffInspection request, CancellationToken ct) =>
        CoreAsync(new SqliteSchemaRead(connection, transaction, request), ct);

    private static async Task<SchemaDiffObservation> CoreAsync(SqliteSchemaRead read, CancellationToken ct)
    {
        var databases = await SqliteSchemaAdmission.ValidateAsync(read, ct);
        var schema = await read.RowsAsync(Data.Schema, [],
            read.Request.Target.Limits.MaxCatalogObjects, ct);
        foreach (var _ in schema) read.Object();
        var tables = new List<SchemaObservedTable>();
        foreach (var table in read.Request.Target.Scope.OwnedTables)
            tables.Add(await SqliteSchemaTable.ReadAsync(read, table, schema, ct));
        var history = await HistoryAsync(read, schema, ct);
        var mainFile = (string)databases.Single(d => (string)d[0]! == "main")[1]!;
        var actual = SqliteFileIdentity.Canonical(Path.GetFullPath(mainFile));
        var identity = SqliteSchemaRead.Hash("main:" + actual.Length + ":" + actual);
        return SchemaDiffObservation.FromFacts(new(identity, "sqlite:schema-diff-v1", true),
            tables, history, read.Request);
    }

    private static async Task<SchemaObservationHistory> HistoryAsync(SqliteSchemaRead read,
        IReadOnlyList<object?[]> schema, CancellationToken ct)
    {
        var history = schema.SingleOrDefault(r => string.Equals(
            (string)r[1]!, "schema_migrations", StringComparison.OrdinalIgnoreCase));
        if (history is null) return new(SchemaFact<bool>.Known(false), SchemaFact<IReadOnlyList<string>>.Known([]));
        if ((string)history[1]! != "schema_migrations") throw new SchemaDiffException(SchemaDiffError.Ownership);
        if ((string)history[0]! != "table" || SqliteSchemaDefinition.IsVirtual((string?)history[3]))
            return new(SchemaFact<bool>.Unsupported("history-kind"),
            SchemaFact<IReadOnlyList<string>>.Unknown("history-kind"));
        if (!await HistoryLayoutAsync(read, history, schema, ct))
            return new(SchemaFact<bool>.Known(true),
                SchemaFact<IReadOnlyList<string>>.Unsupported("history-layout-or-effects"));
        var versions = await read.RowsAsync(Data.HistoryVersions, [],
            read.Request.Target.Limits.MaxCatalogObjects, ct);
        return new(SchemaFact<bool>.Known(true),
            SchemaFact<IReadOnlyList<string>>.Known(versions.Select(r => (string)r[0]!).ToArray()));
    }
    private static async Task<bool> HistoryLayoutAsync(SqliteSchemaRead read, object?[] history,
        IReadOnlyList<object?[]> schema, CancellationToken ct)
    {
        var definition = SqliteSchemaDefinition.Parse((string?)history[3]);
        var columns = await SqliteSchemaColumns.ReadAsync(read, MigrationRunner.HistoryTable, definition, ct);
        if (!SqliteHistoryLayout.Valid(columns)) return false;
        var facts = await SqliteSchemaInventory.ReadAsync(read, MigrationRunner.HistoryTable, definition, schema, ct);
        return Empty(facts.Constraints) && Empty(facts.Triggers) && Empty(facts.DependentObjects);
    }

    private static bool Empty(SchemaFact<IReadOnlyList<SchemaObjectStamp>> fact) =>
        fact.Knowledge == SchemaKnowledge.Known && fact.GetKnown().Count == 0;

}
