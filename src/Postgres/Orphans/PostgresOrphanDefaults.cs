using System.Globalization;
using Neemle.OrmLite.Nodes;
using L = Neemle.OrmLite.Postgres.PostgresOrphanDataLiterals;

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
        ColumnType.Text => expression == Literal(25, -1, L.EmptyTextDatum),
        ColumnType.Bool => expression == Zero(16, 1),
        ColumnType.Int32 => expression == Zero(23, 4),
        ColumnType.Int64 => Numeric(expression, 20, 481),
        ColumnType.Double => Numeric(expression, 701, 316),
        ColumnType.Json => expression == Literal(3802, -1, L.EmptyJsonDatum),
        _ => false,
    };

    private static bool Numeric(string expression, int type, int conversion) =>
        expression == Zero(type, 8) || expression == Coercion(type, conversion);

    private static string Zero(int type, int length) => Literal(type, length,
        string.Format(CultureInfo.InvariantCulture, L.ZeroDatum, length));

    private static string Literal(int type, int length, string value)
    {
        var collation = type == 25 ? 100 : 0;
        var byValue = length == -1 ? "false" : "true";
        return string.Format(CultureInfo.InvariantCulture, L.ConstNode, type, collation,
            length, byValue, value);
    }

    private static string Coercion(int type, int conversion) => string.Format(CultureInfo.InvariantCulture,
        L.CoercionNode, conversion, type, Zero(23, 4));
}
