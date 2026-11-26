namespace Neemle.OrmLite.Sqlite;

internal static class SqliteSchemaReferences
{
    internal static async Task<SchemaFact<IReadOnlyList<SchemaObjectStamp>>> ReadAsync(SqliteSchemaRead read,
        string table, IReadOnlyList<object?[]> schema, CancellationToken ct)
    {
        if (schema.Any(r => (string)r[0]! == "view" || SqliteSchemaDefinition.IsVirtual((string?)r[3])
            || (string)r[0]! == "trigger" && (string)r[2]! != table))
            return SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Unknown("external-definition-dependencies");
        var stamps = new List<SchemaObjectStamp>();
        foreach (var source in schema.Where(r => (string)r[0]! == "table"))
        {
            var name = (string)source[1]!;
            var keys = await read.RowsAsync("SELECT * FROM pragma_foreign_key_list(@table,'main')",
                SqliteSchemaRead.Parameter("table", name), read.Request.Target.Limits.MaxCatalogObjects, ct);
            foreach (var key in keys.Where(k => string.Equals((string)k[2]!, table, StringComparison.OrdinalIgnoreCase)))
                stamps.Add(new("incoming-foreign-key", name + ":" + key[0] + ":" + key[1],
                    SqliteSchemaRead.Hash(string.Join("|", key))));
        }
        return SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known(stamps);
    }
}
