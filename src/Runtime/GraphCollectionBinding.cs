namespace Neemle.OrmLite;

public sealed class GraphCollectionBinding<TSource, TTarget, TKey> : GraphNavigationBinding<TSource>
    where TSource : class where TTarget : class where TKey : notnull
{
    private readonly Func<TSource, TKey> _sourceKey;
    private readonly Func<TTarget, GraphKey<TKey>> _targetKey;
    private readonly Action<TSource, List<TTarget>> _assign;

    public GraphCollectionBinding(string navigation, string targetForeignKeyColumn, Func<TSource, TKey> sourceKey,
        Func<TTarget, GraphKey<TKey>> targetForeignKey, Action<TSource, List<TTarget>> assign)
    {
        Navigation = navigation;
        TargetColumn = targetForeignKeyColumn;
        _sourceKey = sourceKey ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        _targetKey = targetForeignKey ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        _assign = assign ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        Require(!string.IsNullOrWhiteSpace(navigation) && !string.IsNullOrWhiteSpace(targetForeignKeyColumn));
    }

    internal override Type Target => typeof(TTarget);
    internal override string Navigation { get; }
    internal override string? SourceColumn => null;
    internal override string TargetColumn { get; }
    internal override Type SourceKeyType => typeof(TKey);
    internal override Type TargetKeyType => typeof(TKey);

    internal override bool Equivalent(IGraphNavigationBinding other) =>
        other is GraphCollectionBinding<TSource, TTarget, TKey> typed && Navigation == typed.Navigation
        && TargetColumn == typed.TargetColumn && _sourceKey.Equals(typed._sourceKey)
        && _targetKey.Equals(typed._targetKey) && _assign.Equals(typed._assign);

    internal override void Validate(IGraphEntityBinding source, IGraphEntityBinding target)
    {
        Require(source.KeyType == typeof(TKey) && HasColumn(target, TargetColumn));
        GraphFrontier.ColumnsMatch(source, source.KeyColumn, target, TargetColumn);
    }

    internal override async Task<List<object>> LoadAsync(GraphReadContext context,
        GraphPlanNode node, IReadOnlyList<object> parents)
    {
        var sources = parents.Select(GraphFrontier.Typed<TSource>).ToList();
        var keys = sources.Select(_sourceKey).Distinct().Cast<object>().ToList();
        Require(keys.All(k => k is TKey));
        var rows = await GraphFrontier.ReadAsync(context, node, TargetColumn, keys);
        var groups = Group(rows);
        foreach (var source in sources)
            _assign(source, groups.TryGetValue(_sourceKey(source), out var group) ? new List<TTarget>(group) : []);
        return rows.Distinct(ReferenceEqualityComparer.Instance).ToList();
    }

    private Dictionary<TKey, List<TTarget>> Group(IEnumerable<object> rows)
    {
        var groups = new Dictionary<TKey, List<TTarget>>();
        foreach (var row in rows.Distinct(ReferenceEqualityComparer.Instance))
        {
            var target = GraphFrontier.Typed<TTarget>(row);
            var key = _targetKey(target);
            if (!key.HasValue) continue;
            if (key.Value is null) throw new GraphRequestException(GraphRequestError.InvalidBinding);
            if (!groups.TryGetValue(key.Value, out var group)) groups.Add(key.Value, group = []);
            group.Add(target);
        }
        return groups;
    }
}
