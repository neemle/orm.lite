using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class GraphEntitySnapshot
{
    internal GraphEntitySnapshot(IGraphEntityBinding binding)
    {
        Binding = binding;
        Table = binding.Descriptor.Table;
        Columns = binding.Descriptor.Columns.ToArray();
        Calculated = binding.Descriptor.Calculated.ToArray();
        SoftColumn = binding.Descriptor.SoftColumn;
        Navigations = binding.Navigations.ToArray();
    }

    internal IGraphEntityBinding Binding { get; }
    internal string Table { get; }
    internal ColumnDescriptor[] Columns { get; }
    internal CalculatedDescriptor[] Calculated { get; }
    internal string? SoftColumn { get; }
    internal IGraphNavigationBinding[] Navigations { get; }

    internal NodeList Projection(IReadOnlyDictionary<string, Node> masks)
    {
        var physical = Columns.Select(c => masks.TryGetValue(c.Column, out var mask)
            ? (Node)new Aliased(mask, c.Column) : new ColumnRef(c.Column));
        var calculated = Calculated.Select(c => (Node)new Aliased(
            new Call(Fn.Lower, [new ColumnRef(c.SourceColumn)]), c.Alias));
        if (Calculated.Any(c => masks.ContainsKey(c.SourceColumn)))
            throw new GraphRequestException(GraphRequestError.UnsupportedShape);
        return NodeList.Of(physical.Concat(calculated));
    }
}
