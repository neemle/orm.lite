namespace Neemle.OrmLite;

public sealed class GraphLinkBinding<TSource, TTarget, TSourceKey, TTargetKey> : GraphNavigationBinding<TSource>
    where TSource : class where TTarget : class where TSourceKey : notnull where TTargetKey : notnull
{
    private readonly Func<TSource, TSourceKey> _sourceKey;
    private readonly Action<TSource, List<TTarget>> _assign;

    public GraphLinkBinding(string navigation, GraphLinkSource source,
        Func<TSource, TSourceKey> sourceKey, Action<TSource, List<TTarget>> assign)
    {
        Navigation = navigation;
        Link = source ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        _sourceKey = sourceKey ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        _assign = assign ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        Require(!string.IsNullOrWhiteSpace(navigation) && !string.IsNullOrWhiteSpace(source.Table)
            && !string.IsNullOrWhiteSpace(source.SourceColumn) && !string.IsNullOrWhiteSpace(source.TargetColumn));
    }

    internal override Type Target => typeof(TTarget);
    internal override string Navigation { get; }
    internal override string? SourceColumn => null;
    internal override string? TargetColumn => null;
    internal override Type SourceKeyType => typeof(TSourceKey);
    internal override Type TargetKeyType => typeof(TTargetKey);
    internal override GraphLinkSource Link { get; }

    internal override bool Equivalent(IGraphNavigationBinding other) =>
        other is GraphLinkBinding<TSource, TTarget, TSourceKey, TTargetKey> typed
        && Navigation == typed.Navigation && Link == typed.Link
        && _sourceKey.Equals(typed._sourceKey) && _assign.Equals(typed._assign);

    internal override void Validate(IGraphEntityBinding source, IGraphEntityBinding target) =>
        Require(source.KeyType == typeof(TSourceKey) && target.KeyType == typeof(TTargetKey));

    internal override async Task<List<object>> LoadAsync(GraphReadContext context,
        GraphPlanNode node, IReadOnlyList<object> parents)
    {
        var sources = parents.Select(GraphFrontier.Typed<TSource>).ToList();
        var keys = sources.Select(_sourceKey).Distinct().Cast<object>().ToList();
        Require(keys.All(k => k is TSourceKey));
        var links = await GraphLinkReader.ReadAsync<TSource, TSourceKey, TTargetKey>(context, node, keys);
        var targetKeys = links.Select(p => p.Target).Distinct().Cast<object>().ToList();
        var rows = await GraphFrontier.ReadAsync(context, node, node.Target.Binding.KeyColumn, targetKeys);
        var targets = rows.Distinct(ReferenceEqualityComparer.Instance).ToDictionary(
            node.Target.Binding.Key, GraphFrontier.Typed<TTarget>, node.Target.Binding.Comparer);
        var groups = links.ToLookup(p => p.Source, p => p.Target);
        foreach (var source in sources)
            _assign(source, groups[_sourceKey(source)].Distinct().Where(k => targets.ContainsKey(k))
                .Select(k => targets[k]).ToList());
        return rows.Distinct(ReferenceEqualityComparer.Instance).ToList();
    }
}
