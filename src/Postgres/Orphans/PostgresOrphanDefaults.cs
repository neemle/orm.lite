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
        ColumnType.Text => expression == Literal(25, -1, "4 [ 16 0 0 0 ]"),
        ColumnType.Bool => expression == Zero(16, 1),
        ColumnType.Int32 => expression == Zero(23, 4),
        ColumnType.Int64 => Numeric(expression, 20, 481),
        ColumnType.Double => Numeric(expression, 701, 316),
        ColumnType.Json => expression == Literal(3802, -1, "8 [ 32 0 0 0 0 0 0 32 ]"),
        _ => false,
    };

    private static bool Numeric(string expression, int type, int conversion) =>
        expression == Zero(type, 8) || expression == Coercion(type, conversion);

    private static string Zero(int type, int length) =>
        Literal(type, length, FormattableString.Invariant($"{length} [ 0 0 0 0 0 0 0 0 ]"));

    private static string Literal(int type, int length, string value)
    {
        var collation = type == 25 ? 100 : 0;
        var byValue = length == -1 ? "false" : "true";
        var metadata = FormattableString.Invariant(
            $"{{CONST :consttype {type} :consttypmod -1 :constcollid {collation} ");
        var storage = FormattableString.Invariant($":constlen {length} :constbyval {byValue} ");
        return metadata + storage + $":constisnull false :location -1 :constvalue {value}}}";
    }

    private static string Coercion(int type, int conversion) => FormattableString.Invariant(
        $"{{FUNCEXPR :funcid {conversion} :funcresulttype {type} :funcretset false :funcvariadic false ")
        + $":funcformat 2 :funccollid 0 :inputcollid 0 :args ({Zero(23, 4)}) :location -1}}";
}
