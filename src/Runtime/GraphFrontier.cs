using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class GraphFrontier
{
    internal static T Typed<T>(object value) where T : class => value is T typed ? typed
        : throw new GraphRequestException(GraphRequestError.InvalidBinding);

    internal static async Task<List<object>> ReadAsync(GraphReadContext context, GraphPlanNode node,
        string column, IReadOnlyList<object> keys)
    {
        var result = new List<object>();
        var capacity = context.Database.Provider.MaxBindParameters - node.ReservedParameters;
        foreach (var chunk in keys.Chunk(capacity))
        {
            var shape = Filter(node.Shape, column, chunk, context.Budget.RelatedRemaining);
            var statement = StatementCompiler.Compile(shape, context.Database.Provider,
                StatementKind.Read, node.Target.Table);
            var rows = await GraphStatementRunner.ReadAsync(context, statement,
                r => Map(context, node, r));
            result.AddRange(rows);
        }
        return result;
    }

    private static object Map(GraphReadContext context, GraphPlanNode node, System.Data.Common.DbDataReader reader)
    {
        var mapped = node.Target.Binding.Map(reader);
        GraphSaveOrigins.Observe(mapped, node.Target.Binding.OriginalMapper, node.MayBePartial);
        var canonical = context.Identity.Intern(node.Target.Binding, mapped);
        GraphSaveOrigins.Observe(canonical, node.Target.Binding.OriginalMapper, node.MayBePartial);
        OrphanReadOrigins.ObserveMapped(canonical, node.Target.Binding, node.MayBePartial);
        return canonical;
    }

    internal static Select Filter(Select shape, string column, object[] keys, long remaining)
    {
        var ordinal = NextOrdinal(shape);
        var values = keys.Select((value, index) => (Node)new Param(ordinal + index, value));
        var terms = shape.Where?.Terms.ToList() ?? [];
        terms.Add(new InList(new ColumnRef(column), NodeList.Of(values)));
        return shape with { Where = new Where(NodeList.Of(terms)),
            Limit = remaining >= int.MaxValue ? null : new Limit((int)(remaining + 1)) };
    }

    private static int NextOrdinal(Node node)
    {
        var next = node is Param parameter ? parameter.Ordinal + 1 : 0;
        foreach (var child in node.Children()) next = Math.Max(next, NextOrdinal(child));
        return next;
    }

    internal static void ColumnsMatch(IGraphEntityBinding left, string leftColumn,
        IGraphEntityBinding right, string rightColumn)
    {
        var a = left.Descriptor.Columns.SingleOrDefault(c => c.Column == leftColumn);
        var b = right.Descriptor.Columns.SingleOrDefault(c => c.Column == rightColumn);
        if (a is null || b is null || a.Type != b.Type)
            throw new GraphRequestException(GraphRequestError.InvalidBinding);
    }
}
