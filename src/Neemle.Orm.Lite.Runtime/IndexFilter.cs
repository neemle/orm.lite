using System.Globalization;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public enum IndexFilterOp
{
    IsNull,
    NotNull,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}

public readonly record struct IndexCondition(string Column, IndexFilterOp Op, object? Value = null)
{
    public static IndexCondition Null(string column) => new(column, IndexFilterOp.IsNull);

    public static IndexCondition NotNull(string column) => new(column, IndexFilterOp.NotNull);

    public static IndexCondition Of(string column, IndexFilterOp op, string value) => new(column, op, value);

    public static IndexCondition Of(string column, IndexFilterOp op, long value) => new(column, op, value);

    public static IndexCondition Of(string column, IndexFilterOp op, bool value) => new(column, op, value);

    public Node ToNode()
    {
        var column = new ColumnRef(Column);
        return Op switch
        {
            IndexFilterOp.IsNull => new IsNull(column),
            IndexFilterOp.NotNull => new IsNotNull(column),
            _ => new Compare(Operator(Op), column, Literal(Value)),
        };
    }

    public string Identity => Column + " " + Op + " " + Convert.ToString(Value, CultureInfo.InvariantCulture);

    private static CompareOp Operator(IndexFilterOp op) => op switch
    {
        IndexFilterOp.Equal => CompareOp.Equal,
        IndexFilterOp.NotEqual => CompareOp.NotEqual,
        IndexFilterOp.Less => CompareOp.Less,
        IndexFilterOp.LessOrEqual => CompareOp.LessOrEqual,
        IndexFilterOp.Greater => CompareOp.Greater,
        _ => CompareOp.GreaterOrEqual,
    };

    private static Node Literal(object? value) => value switch
    {
        string text => new StringLit(text),
        bool flag => new BoolLit(flag),
        long number => new IntLit(number),
        _ => throw new ArgumentException("an index condition compares with text, a number or a flag.", nameof(value)),
    };
}
