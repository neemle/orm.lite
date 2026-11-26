namespace Neemle.OrmLite.Sqlite;

internal static class SqliteSchemaTypes
{
    internal static SchemaFact<SchemaStorageType> Parse(string native) => native.ToUpperInvariant() switch
    {
        "TEXT" => SchemaFact<SchemaStorageType>.Known(new("text", null, null)),
        "INTEGER" => SchemaFact<SchemaStorageType>.Known(new("integer", null, null)),
        "REAL" => SchemaFact<SchemaStorageType>.Known(new("real", null, null)),
        _ => SchemaFact<SchemaStorageType>.Unsupported("native-type-grammar"),
    };

    internal static SchemaFact<SchemaDefault> Default(string? text) => text switch
    {
        null => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.None)),
        "0" => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.Zero)),
        "FALSE" or "false" => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.False)),
        "''" => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.EmptyText)),
        "CURRENT_TIMESTAMP" => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.CurrentTime)),
        _ => SchemaFact<SchemaDefault>.Unsupported("default-grammar"),
    };
}
