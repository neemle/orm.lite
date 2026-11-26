using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaCreatedInventory
{
    internal static SchemaObservedTableFacts Facts(SchemaDesiredTable table, string profile)
    {
        if (profile == "postgres:schema-diff-v1") return Postgres(table);
        if (profile == "sqlite:schema-diff-v1") return Sqlite(table);
        throw new SchemaDiffException(SchemaDiffError.UnsupportedProvider);
    }

    private static SchemaObservedTableFacts Postgres(SchemaDesiredTable table)
    {
        var constraints = new List<SchemaObjectStamp>(); var indexes = new List<SchemaObjectStamp>();
        var dependent = new List<SchemaObjectStamp> { SchemaLocalRoles.RowType() };
        if (table.Columns.Any(c => c.Type == ColumnType.Text)) dependent.Add(SchemaLocalRoles.Toast());
        foreach (var column in table.Columns)
        {
            if (!column.Nullable || column.Key) constraints.Add(SchemaLocalRoles.Constraint(table.Name, column, "nn"));
            if (column.Key)
            {
                constraints.Add(SchemaLocalRoles.Constraint(table.Name, column, "pk"));
                indexes.Add(SchemaLocalRoles.Index(table.Name, column));
            }
            if (SchemaPhysicalTypes.Default(column) != SchemaDefaultKind.None)
                dependent.Add(SchemaLocalRoles.Default(column.Column));
        }
        dependent.AddRange(constraints.Select(SchemaLocalRoles.ConstraintDependency));
        return new(Known(indexes), Known(constraints), Known([]), Known(dependent));
    }

    private static SchemaObservedTableFacts Sqlite(SchemaDesiredTable table)
    {
        var key = table.Columns.SingleOrDefault(c => c.Key);
        var indexes = new List<SchemaObjectStamp>();
        if (key is not null && key.Type is not (ColumnType.Int32 or ColumnType.Int64))
            indexes.Add(new("index:pk", "sqlite_autoindex_" + table.Name + "_1",
                SchemaLocalRoles.Digest("sqlite-primary-index-v1", key.Column)));
        return new(Known(indexes), Known([]), Known([]), Known([]));
    }

    private static SchemaFact<IReadOnlyList<SchemaObjectStamp>> Known(IReadOnlyList<SchemaObjectStamp> stamps) =>
        SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known(stamps);
}
