using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class JoinProjectionCompiler(JoinCompilationContext context, JoinQueryPlan plan)
{
    private readonly List<Node> _columns = [];
    private readonly List<string> _names = [];

    internal NodeList Columns => NodeList.Of(_columns);

    internal JoinedReadLayout Build(JoinSelection selection)
    {
        var entries = selection.Snapshot();
        if (entries.Count == 0) throw new JoinReadException(JoinError.InvalidProjection);
        if (entries.Count > plan.Limits.MaxProjectionColumns) throw new JoinReadException(JoinError.LimitExceeded);
        foreach (var entry in entries) Project(entry);
        var sentinels = Presence();
        var cells = entries.Select((entry, index) => new JoinLayoutCell(entry, index,
            sentinels.TryGetValue(entry.Column.Source, out var ordinal) ? ordinal : null)).ToArray();
        return new JoinedReadLayout(selection.Identity, Array.AsReadOnly(cells), Array.AsReadOnly(_names.ToArray()));
    }

    private void Project(JoinSelectionEntry entry)
    {
        var nullable = plan.Edges.Any(e => e.Left && ReferenceEquals(e.Source, entry.Column.Source));
        if (entry.Required && (nullable || entry.Column.Binding.Column.Nullable))
            throw new JoinReadException(JoinError.RequiredNullable);
        Add(context.Column(entry.Column, true));
    }

    private Dictionary<QueryJoinSnapshot, int> Presence()
    {
        var ordinals = new Dictionary<QueryJoinSnapshot, int>(ReferenceEqualityComparer.Instance);
        foreach (var edge in plan.Edges.Where(e => e.Left))
        {
            ordinals.Add(edge.Source, _columns.Count);
            Add(new ColumnRef(edge.Source.Metadata.KeyColumn, new AliasRef(context.Alias(edge.Source))));
        }
        return ordinals;
    }

    private void Add(Node expression)
    {
        if (_columns.Count == plan.Limits.MaxProjectionColumns)
            throw new JoinReadException(JoinError.LimitExceeded);
        var name = context.OutputName(_columns.Count);
        _columns.Add(new Aliased(expression, name));
        _names.Add(name);
    }
}
