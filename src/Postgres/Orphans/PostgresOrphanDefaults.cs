using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Postgres;

internal static class PostgresOrphanDefaults
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
        ColumnType.Text => expression == "''::text",
        ColumnType.Bool => expression == "false",
        ColumnType.Int32 => expression is "0" or "0::integer",
        ColumnType.Int64 => expression is "0" or "0::bigint",
        ColumnType.Double => expression is "0" or "0::double precision",
        ColumnType.Json => expression == "'{}'::jsonb",
        _ => false,
    };
}
