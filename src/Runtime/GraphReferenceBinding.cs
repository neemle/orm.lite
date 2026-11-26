namespace Neemle.OrmLite;

public sealed class GraphReferenceBinding<TSource, TTarget, TKey> : GraphNavigationBinding<TSource>
    where TSource : class where TTarget : class where TKey : notnull
{
    private readonly Func<TSource, GraphKey<TKey>> _key;
    private readonly Action<TSource, TTarget?> _assign;

    public GraphReferenceBinding(string navigation, string sourceForeignKeyColumn,
        Func<TSource, GraphKey<TKey>> sourceForeignKey, Action<TSource, TTarget?> assign)
    {
        Navigation = navigation;
        SourceColumn = sourceForeignKeyColumn;
        _key = sourceForeignKey ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        _assign = assign ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        Require(!string.IsNullOrWhiteSpace(navigation) && !string.IsNullOrWhiteSpace(sourceForeignKeyColumn));
    }

    internal override object CaptureSourceKey(TSource source) => _key(source);

    internal override Type Target => typeof(TTarget);
    internal override string Navigation { get; }
    internal override string SourceColumn { get; }
    internal override string? TargetColumn => null;
    internal override Type SourceKeyType => typeof(TKey);

    internal override Type TargetKeyType => typeof(TKey);

    internal override bool Equivalent(IGraphNavigationBinding other) =>
        other is GraphReferenceBinding<TSource, TTarget, TKey> typed
        && Navigation == typed.Navigation && SourceColumn == typed.SourceColumn
        && _key.Equals(typed._key) && _assign.Equals(typed._assign);

    internal override void Validate(IGraphEntityBinding source, IGraphEntityBinding target)
    {
        Require(target.KeyType == typeof(TKey) && HasColumn(source, SourceColumn));
        GraphFrontier.ColumnsMatch(source, SourceColumn, target, target.KeyColumn);
    }

    internal override async Task<List<object>> LoadAsync(GraphReadContext context,
        GraphPlanNode node, IReadOnlyList<object> parents)
    {
        var sources = parents.Select(GraphFrontier.Typed<TSource>).ToList();
        var memberships = sources.Select(p => (Parent: p, Key: _key(p))).ToList();
        var keys = memberships.Where(p => p.Key.HasValue).Select(p => p.Key.Value).Distinct().Cast<object>().ToList();
        Require(keys.All(k => k is TKey));
        var rows = await GraphFrontier.ReadAsync(context, node, node.Target.Binding.KeyColumn, keys);
        var byKey = rows.Distinct(ReferenceEqualityComparer.Instance).ToDictionary(
            node.Target.Binding.Key, GraphFrontier.Typed<TTarget>, node.Target.Binding.Comparer);
        foreach (var member in memberships)
        {
            var value = member.Key.HasValue && byKey.TryGetValue(member.Key.Value, out var target) ? target : null;
            context.Assign(member.Parent, this, value, () => _assign(member.Parent, value));
        }
        return rows.Distinct(ReferenceEqualityComparer.Instance).ToList();
    }
}
