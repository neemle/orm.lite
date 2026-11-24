using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public static class StatementCompiler
{
    public static Statement Compile(Node tree, ISqlProvider provider, StatementKind kind, string? table = null)
    {
        if (tree is null) throw new ArgumentNullException(nameof(tree));
        if (provider is null) throw new ArgumentNullException(nameof(provider));
        RequireCapabilities(tree, provider, table);
        var lowered = provider.Lower(tree);
        var written = provider.CreateSerializer().Serialize(lowered);
        return new Statement(written.Text, Ordered(written.Parameters), kind);
    }

    public static IReadOnlyList<Capability> RequiredBy(Node tree)
    {
        var found = new List<Capability>();
        Walk(tree, node =>
        {
            foreach (var capability in node.Requires())
                if (!found.Contains(capability)) found.Add(capability);
        });
        return found;
    }

    private static void RequireCapabilities(Node tree, ISqlProvider provider, string? table)
    {
        Walk(tree, node =>
        {
            foreach (var capability in node.Requires())
                if (provider.Capabilities.Of(capability) == Support.None)
                    throw new CapabilityNotSupportedException(provider.Id, node.Name, capability, table);
        });
    }

    private static void Walk(Node node, Action<Node> visit)
    {
        visit(node);
        foreach (var child in node.Children()) Walk(child, visit);
    }

    private static IReadOnlyList<StatementParameter> Ordered(IReadOnlyList<StatementParameter> written)
    {
        var byName = new Dictionary<string, StatementParameter>(StringComparer.Ordinal);
        var kept = new List<StatementParameter>(written.Count);
        foreach (var parameter in written)
        {
            if (byName.TryGetValue(parameter.Name, out var seen))
            {
                if (!ParamValues.AreEqual(seen.Value, parameter.Value))
                    throw new InvalidOperationException(
                        $"parameter {parameter.Name} is written with two different values.");
                continue;
            }
            byName.Add(parameter.Name, parameter);
            kept.Add(parameter);
        }
        return kept.OrderBy(p => p.Ordinal).ToList();
    }
}
