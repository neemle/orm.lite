using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sqlite;

internal static class SqliteOrphanDefaults
{
    internal static void Validate(EntityDescriptor entity, string name, string? expression)
    {
        if (expression is null) return;
        var column = entity.Columns.SingleOrDefault(c => c.Column == name);
        if (column is null || column.Key || column.Nullable || !Constant(column.Type, expression))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private static bool Constant(ColumnType type, string expression) => type switch
    {
        ColumnType.Text => expression == "''",
        ColumnType.Bool => expression == "false",
        ColumnType.Int32 or ColumnType.Int64 or ColumnType.Double => expression == "0",
        ColumnType.Json => expression == "'{}'",
        _ => false,
    };
}
