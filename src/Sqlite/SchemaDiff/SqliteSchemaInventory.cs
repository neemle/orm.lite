namespace Neemle.OrmLite.Sqlite;

internal static class SqliteSchemaInventory
{
    internal static async Task<SchemaObservedTableFacts> ReadAsync(SqliteSchemaRead read,
        string table, ParsedTable? definition, IReadOnlyList<object?[]> schema, CancellationToken ct)
    {
        var indexes = await IndexesAsync(read, table, schema, ct);
        var constraints = Constraints(definition);
        var triggers = schema.Where(r => (string)r[0]! == "trigger" && (string)r[2]! == table)
            .Select(r => new SchemaObjectStamp("trigger", (string)r[1]!,
                SqliteSchemaRead.Hash((string)r[3]!))).ToArray();
        var dependent = await SqliteSchemaReferences.ReadAsync(read, table, schema, ct);
        return new(indexes, constraints, SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known(triggers), dependent);
    }

    private static SchemaFact<IReadOnlyList<SchemaObjectStamp>> Constraints(ParsedTable? table)
    {
        if (table is null) return SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Unknown("table-definition");
        if (table.Tail.TrimEnd(';').Trim().Length > 0)
            return SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Unsupported("table-options");
        var effects = table.Definitions.Where(d => d.Kind == DefinitionKind.Constraint
            || d.Tokens.Any(t => t.IsWord("CHECK") || t.IsWord("UNIQUE") || t.IsWord("REFERENCES")))
            .Select(d => new SchemaObjectStamp("constraint", d.Name ?? SqliteSchemaDefinition.Stamp(d),
                SqliteSchemaDefinition.Stamp(d))).ToArray();
        return SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known(effects);
    }

    private static async Task<SchemaFact<IReadOnlyList<SchemaObjectStamp>>> IndexesAsync(
        SqliteSchemaRead read, string table, IReadOnlyList<object?[]> schema, CancellationToken ct)
    {
        var indexes = await read.RowsAsync("SELECT name,\"unique\",origin,partial "
            + "FROM pragma_index_list(@table,'main')",
            SqliteSchemaRead.Parameter("table", table), read.Request.Target.Limits.MaxCatalogObjects, ct);
        var stamps = new List<SchemaObjectStamp>();
        foreach (var index in indexes)
        {
            var terms = await read.RowsAsync("SELECT seqno,cid,name,desc,coll,\"key\" "
                + "FROM pragma_index_xinfo(@index,'main')", SqliteSchemaRead.Parameter("index", (string)index[0]!),
                read.Request.Target.Limits.MaxCatalogObjects, ct);
            var definition = schema.SingleOrDefault(r => (string)r[0]! == "index"
                && (string)r[1]! == (string)index[0]!);
            var text = string.Join("|", index) + ";" + (string?)definition?[3]
                + ";" + string.Join(";", terms.Select(t => string.Join("|", t)));
            stamps.Add(new("index:" + (string)index[2]!, (string)index[0]!, SqliteSchemaRead.Hash(text)));
        }
        return SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known(stamps);
    }
}
