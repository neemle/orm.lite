using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class JoinSourceNodes
{
    internal static Select Copy(Select root, JoinEntityMetadata metadata,
        IReadOnlyDictionary<string, IJoinColumnBinding> columns)
    {
        Validate(root, metadata);
        if (root.Where is { } where) JoinWhereNodes.Validate(where, metadata, columns);
        var orders = root.OrderBy?.Terms.Select(n => Order(n, metadata, columns)).ToArray();
        return root with
        {
            Columns = NodeList.Empty,
            Where = root.Where is null ? null : new Where(NodeList.Of(root.Where.Terms)),
            OrderBy = orders is null ? null : new OrderBy(NodeList.Of(orders)),
        };
    }

    private static void Validate(Select root, JoinEntityMetadata metadata)
    {
        if (root.Lock is not null || root.Joins is not null || root.GroupBy is not null || root.Having is not null)
            throw new JoinReadException(JoinError.UnsupportedOperation);
        if (root.From is not { Bare: false, Alias: null } from || from.Table != metadata.Table)
            throw new JoinReadException(JoinError.UnsupportedSource);
        if (root.Limit is { Count: < 0 } || root.Offset is { Count: < 0 })
            throw new JoinReadException(JoinError.InvalidRequest);
    }

    private static Node Order(Node node, JoinEntityMetadata metadata,
        IReadOnlyDictionary<string, IJoinColumnBinding> columns)
    {
        if (node is not OrderTerm { Collation: null, Operand: ColumnRef column } order)
            throw new JoinReadException(JoinError.UnsupportedExpression);
        var binding = JoinWhereNodes.Column(column, metadata, columns);
        JoinScalarTypes.Ordered(binding.ValueType);
        return order with { Operand = column with { } };
    }
}
