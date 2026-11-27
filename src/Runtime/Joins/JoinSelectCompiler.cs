using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class JoinSelectCompiler(JoinQueryPlan plan, JoinCompilationContext context)
{
    private readonly JoinExpressionCompiler _expressions = new(context);

    internal Select Build(NodeList columns) => new(columns, context.Table(plan.Root),
        ResultWhere(), Ordering(), plan.EffectiveLimit is { } take ? new Limit(take) : null,
        plan.EffectiveOffset is { } skip ? new Offset(skip) : null, Joins: Joins());

    private NodeList Joins()
    {
        var nodes = new List<Node>();
        foreach (var edge in plan.Edges)
        {
            var on = _expressions.Predicate(edge.On);
            if (edge.Source.Root.Where is { } where)
                foreach (var term in where.Terms) on = new And(on, _expressions.Source(term, edge.Source));
            var table = context.Table(edge.Source);
            nodes.Add(edge.Left ? new LeftJoin(table, on) : new InnerJoin(table, on));
        }
        return NodeList.Of(nodes);
    }

    private Where? ResultWhere()
    {
        var terms = new List<Node>();
        if (plan.Root.Root.Where is { } where)
            terms.AddRange(where.Terms.Select(n => _expressions.Source(n, plan.Root)));
        terms.AddRange(plan.Filters.Select(_expressions.Predicate));
        return terms.Count == 0 ? null : new Where(NodeList.Of(terms));
    }

    private OrderBy? Ordering()
    {
        var terms = new List<Node>();
        if (!plan.ReplaceOrder && plan.Root.Root.OrderBy is { } captured)
            foreach (var node in captured.Terms)
            {
                var order = (OrderTerm)node;
                terms.Add(order with { Operand = _expressions.Source(order.Operand, plan.Root) });
            }
        foreach (var order in plan.Orders)
            terms.Add(new OrderTerm(context.Column(order.Column, false), order.Descending));
        return terms.Count == 0 ? null : new OrderBy(NodeList.Of(terms));
    }
}
