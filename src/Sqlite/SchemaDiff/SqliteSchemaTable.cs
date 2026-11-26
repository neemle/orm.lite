namespace Neemle.OrmLite.Sqlite;

internal static class SqliteSchemaTable
{
    internal static async Task<SchemaObservedTable> ReadAsync(SqliteSchemaRead read,
        string table, IReadOnlyList<object?[]> schema, CancellationToken ct)
    {
        var matches = schema.Where(r => (string)r[0]! is "table" or "view")
            .Where(r => string.Equals((string)r[1]!, table,
            StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length == 0) return await MissingAsync(read, table, schema, ct);
        if (matches.Length != 1 || (string)matches[0][1]! != table)
            throw new SchemaDiffException(SchemaDiffError.Ownership);
        var own = matches[0];
        var definition = SqliteSchemaDefinition.Parse((string?)own[3]);
        var columns = await SqliteSchemaColumns.ReadAsync(read, table, definition, ct);
        var facts = await SqliteSchemaInventory.ReadAsync(read, table, definition, schema, ct);
        if ((string)own[0]! != "table" || SqliteSchemaDefinition.IsVirtual((string?)own[3])) facts = facts with
            { Constraints = SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Unsupported("relation-kind") };
        return new(table, SchemaFact<bool>.Known(true), columns, facts);
    }

    private static async Task<SchemaObservedTable> MissingAsync(SqliteSchemaRead read,
        string table, IReadOnlyList<object?[]> schema, CancellationToken ct)
    {
        var empty = SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known([]);
        var incoming = await SqliteSchemaReferences.ReadAsync(read, table, schema, ct);
        return new(table, SchemaFact<bool>.Known(false), [], new(empty, empty, empty, incoming));
    }
}
