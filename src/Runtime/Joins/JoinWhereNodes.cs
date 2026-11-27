using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class JoinWhereNodes
{
    internal static void Validate(Where where, JoinEntityMetadata metadata,
        IReadOnlyDictionary<string, IJoinColumnBinding> columns)
    {
        var pending = new Stack<Node>(where.Terms);
        while (pending.TryPop(out var node))
        {
            if (node is And or Or or Not or Group)
                foreach (var child in node.Children()) pending.Push(child);
            else Term(node, metadata, columns);
        }
    }

    private static void Term(Node node, JoinEntityMetadata metadata,
        IReadOnlyDictionary<string, IJoinColumnBinding> columns)
    {
        switch (node)
        {
            case Compare compare: JoinSourceComparison.Validate(compare, metadata, columns); break;
            case IsNull test: _ = Operand(test.Operand, metadata, columns); break;
            case IsNotNull test: _ = Operand(test.Operand, metadata, columns); break;
            case ColumnRef boolean when Column(boolean, metadata, columns).Column.Type == ColumnType.Bool: break;
            case BoolLit: break;
            default: throw new JoinReadException(JoinError.UnsupportedExpression);
        }
    }

    private static IJoinColumnBinding Operand(Node node, JoinEntityMetadata metadata,
        IReadOnlyDictionary<string, IJoinColumnBinding> columns) => node is ColumnRef column
        ? Column(column, metadata, columns) : throw new JoinReadException(JoinError.UnsupportedExpression);

    internal static IJoinColumnBinding Column(ColumnRef column, JoinEntityMetadata metadata,
        IReadOnlyDictionary<string, IJoinColumnBinding> columns)
    {
        if (column.Bare) throw new JoinReadException(JoinError.UnsupportedExpression);
        if (column.Owner is not null && column.Owner is not TableRef { Alias: null, Bare: false })
            throw new JoinReadException(JoinError.UnsupportedExpression);
        if (column.Owner is TableRef table && table.Table != metadata.Table)
            throw new JoinReadException(JoinError.OutOfScopeSource);
        var binding = columns.Values.SingleOrDefault(c => c.Column.Column == column.Column);
        if (binding is null) throw new JoinReadException(JoinError.UnsupportedType);
        if (binding.Calculated) throw new JoinReadException(JoinError.UnsupportedExpression);
        return binding;
    }
}
