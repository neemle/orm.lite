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
        if (plan.Root.Root.Where is { } where) budget.Source(where.Terms.Select(term => (term, 1)));
        foreach (var edge in plan.Edges)
        {
            // Rendered as And(...And(And(ON, t1), t2)..., tn): an And above the typed ON for each captured filter of
            // the right source, so the ON starts n levels down and filter k sits n - k + 2 levels down.
            var terms = edge.Source.Root.Where?.Terms.ToArray() ?? [];
            for (var k = 1; k <= terms.Length; k++) budget.Next(terms.Length - k + 1);
            budget.Typed(edge.On, terms.Length + 1);
            budget.Source(terms.Select((term, i) => (term, terms.Length - i + 1)));
        }
        foreach (var predicate in plan.Filters) budget.Typed(predicate, 1);
    }

    private void Source(IEnumerable<(Node Node, int Depth)> roots)
    {
        var pending = new Stack<(Node Node, int Depth)>(roots);
        while (pending.TryPop(out var entry))
        {
            Next(entry.Depth);
            if (entry.Node is And or Or or Not or Group)
                foreach (var child in entry.Node.Children()) pending.Push((child, entry.Depth + 1));
        }
    }

    private void Typed(JoinPredicate root, int depth)
    {
        var pending = new Stack<(JoinPredicate Node, int Depth)>();
        pending.Push((root, depth));
        while (pending.TryPop(out var entry))
        {
            Next(entry.Depth);
            if (entry.Node.Left is { } left) pending.Push((left, entry.Depth + 1));
            if (entry.Node.Right is { } right) pending.Push((right, entry.Depth + 1));
        }
    }
}
