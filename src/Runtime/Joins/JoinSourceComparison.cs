using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class JoinSourceComparison
{
    internal static void Validate(Compare compare, JoinEntityMetadata metadata,
        IReadOnlyDictionary<string, IJoinColumnBinding> columns)
    {
        if (!Enum.IsDefined(compare.Op)) throw new JoinReadException(JoinError.UnsupportedExpression);
        var column = compare.Left as ColumnRef ?? compare.Right as ColumnRef
            ?? throw new JoinReadException(JoinError.UnsupportedExpression);
        var binding = JoinWhereNodes.Column(column, metadata, columns);
        var ordered = compare.Op is not (CompareOp.Equal or CompareOp.NotEqual);
        if (ordered) JoinScalarTypes.Ordered(binding.ValueType);
        var other = ReferenceEquals(column, compare.Left) ? compare.Right : compare.Left;
        if (other is ColumnRef right)
        {
            var matched = JoinWhereNodes.Column(right, metadata, columns);
            if (matched.ValueType != binding.ValueType || matched.Column.Nullable != binding.Column.Nullable)
                throw new JoinReadException(JoinError.UnsupportedType);
        }
        else Constant(other, binding, ordered);
    }

    private static void Constant(Node node, IJoinColumnBinding binding, bool ordered)
    {
        var value = node switch
        {
            Param p => p.Value, BoolLit b => b.Value, IntLit i => i.Value,
            StringLit s => s.Value, NullLit => null,
            _ => throw new JoinReadException(JoinError.UnsupportedExpression),
        };
        value = JoinScalarTypes.Value(value);
        if (ordered && value is null) throw new JoinReadException(JoinError.UnsupportedType);
        if (value is not null && JoinScalarTypes.Kind(value.GetType()) != binding.Column.Type)
            throw new JoinReadException(JoinError.UnsupportedType);
    }
}
