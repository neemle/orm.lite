using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class JoinPredicateLimits(int maximum)
{
    // How deep a predicate may nest, whatever MaxPredicateNodes allows: compiling and rendering a predicate recurse
    // once per level, so the depth has a bound of its own. This walk runs before either of them.
    internal const int MaxDepth = 128;

    private int _used;

    private void Next(int depth)
    {
        if (_used == maximum || depth > MaxDepth) throw new JoinReadException(JoinError.LimitExceeded);
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
                foreach (var _ in where.Terms) budget.Next(1);
        }
        foreach (var predicate in plan.Filters) budget.Typed(predicate);
    }

    private void Source(IEnumerable<Node> roots)
    {
        var pending = new Stack<(Node Node, int Depth)>(roots.Select(root => (root, 1)));
        while (pending.TryPop(out var entry))
        {
            Next(entry.Depth);
            if (entry.Node is And or Or or Not or Group)
                foreach (var child in entry.Node.Children()) pending.Push((child, entry.Depth + 1));
        }
    }

    private void Typed(JoinPredicate root)
    {
        var pending = new Stack<(JoinPredicate Node, int Depth)>();
        pending.Push((root, 1));
        while (pending.TryPop(out var entry))
        {
            Next(entry.Depth);
            if (entry.Node.Left is { } left) pending.Push((left, entry.Depth + 1));
            if (entry.Node.Right is { } right) pending.Push((right, entry.Depth + 1));
        }
    }
}
