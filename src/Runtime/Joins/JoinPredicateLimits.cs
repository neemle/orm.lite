using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class JoinPredicateLimits(int maximum)
{
    private int _used;

    private void Next()
    {
        if (_used == maximum) throw new JoinReadException(JoinError.LimitExceeded);
        _used++;
    }

    internal static void Validate(JoinQueryPlan plan)
    {
        var budget = new JoinPredicateLimits(plan.Limits.MaxPredicateNodes);
        foreach (var source in plan.Sources)
            if (source.Root.Where is { } where) budget.Source(where.Terms);
        foreach (var edge in plan.Edges)
        {
            budget.Typed(edge.On);
            if (edge.Source.Root.Where is { } where)
                foreach (var _ in where.Terms) budget.Next();
        }
        foreach (var predicate in plan.Filters) budget.Typed(predicate);
    }

    private void Source(IEnumerable<Node> roots)
    {
        var pending = new Stack<Node>(roots);
        while (pending.TryPop(out var node))
        {
            Next();
            if (node is And or Or or Not or Group)
                foreach (var child in node.Children()) pending.Push(child);
        }
    }

    private void Typed(JoinPredicate root)
    {
        var pending = new Stack<JoinPredicate>();
        pending.Push(root);
        while (pending.TryPop(out var node))
        {
            Next();
            if (node.Left is { } left) pending.Push(left);
            if (node.Right is { } right) pending.Push(right);
        }
    }
}
