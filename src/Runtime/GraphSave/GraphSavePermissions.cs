namespace Neemle.OrmLite;

public sealed class GraphSavePermissions
{
    private readonly IReadOnlyDictionary<IGraphSaveRelation, GraphSaveRule> _rules;
    private GraphSavePermissions(IReadOnlyDictionary<IGraphSaveRelation, GraphSaveRule> rules) => _rules = rules;
    public static GraphSavePermissions None { get; } = new(new Dictionary<IGraphSaveRelation, GraphSaveRule>());

    public GraphSavePermissions Allow<TSource, TTarget>(SaveRelation<TSource, TTarget> relation, GraphSaveRule rule)
        where TSource : class where TTarget : class
    {
        if (relation is null || rule is null) throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        var binding = relation.Binding;
        if (binding is null || !Enum.IsDefined(rule.Keys)
            || (rule.Actions & ~(GraphSaveActions.Insert | GraphSaveActions.Update | GraphSaveActions.AddLink)) != 0)
            throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        if (binding.Shape.Kind != GraphSaveRelationKind.Link && rule.Actions.HasFlag(GraphSaveActions.AddLink))
            throw GraphSaveRefusal.At(GraphSaveError.ActionNotAllowed);
        var rules = _rules.ToDictionary(p => p.Key, p => p.Value);
        rules[binding] = rule with { };
        return new GraphSavePermissions(rules);
    }

    internal IReadOnlyDictionary<IGraphSaveRelation, GraphSaveRule> Rules => _rules;
}
