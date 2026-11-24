using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public sealed record ViewDefinition(string View, Select Query)
{
    public IReadOnlyCollection<string> Reads => ViewDefinitions.Reads(Query);

    public string Marker(ISqlProvider provider) => ViewDefinitions.Marker(provider, new CreateView(View, Query));
}

public static class ViewDefinitions
{
    public static string Marker(ISqlProvider provider, CreateView node)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(node);
        return OrmSchema.StableHash([provider.CreateSerializer().Serialize(node with { Replace = false }).Text]);
    }

    internal static Query<T> Source<T>(ISqlProvider provider) => Query<T>.Definition(provider);

    internal static IReadOnlyCollection<string> Reads(Node node)
    {
        var tables = new HashSet<string>(StringComparer.Ordinal);
        Collect(node, tables);
        return tables;
    }

    internal static IReadOnlyList<ViewDefinition> InDependencyOrder(IReadOnlyList<ViewDefinition> views)
    {
        ArgumentNullException.ThrowIfNull(views);
        var names = views.Select(v => v.View).ToList();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Count)
            throw new ArgumentException("a view is named once in a set of views.", nameof(views));
        var ordered = new List<ViewDefinition>();
        var remaining = views.ToList();
        while (remaining.Count > 0)
        {
            var ready = remaining.FirstOrDefault(v => v.Reads.All(r => !names.Contains(r, StringComparer.Ordinal) || ordered.Any(o => o.View == r)))
                ?? throw new InvalidOperationException(
                    "the views " + string.Join(", ", remaining.Select(v => v.View)) + " read each other, so none can be created first.");
            ordered.Add(ready);
            remaining.Remove(ready);
        }
        return ordered;
    }

    private static void Collect(Node node, HashSet<string> tables)
    {
        if (node is TableRef table) tables.Add(table.Table);
        foreach (var child in node.Children()) Collect(child, tables);
    }
}
