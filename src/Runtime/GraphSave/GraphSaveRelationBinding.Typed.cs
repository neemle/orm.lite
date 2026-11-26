namespace Neemle.OrmLite;

public sealed class GraphSaveRelationBinding<T, TTarget> : GraphSaveRelationBinding<T>, IGraphSaveRelation
    where T : class where TTarget : class
{
    private readonly Func<T, IReadOnlyList<TTarget>> _targets;

    public GraphSaveRelationBinding(GraphSaveRelationShape shape, Func<T, IReadOnlyList<TTarget>> suppliedTargets)
    {
        if (shape is null || suppliedTargets is null) throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        GraphSaveShapes.Relation(shape);
        Shape = shape with { };
        _targets = suppliedTargets;
    }

    internal override IGraphSaveRelation Binding => this;
    internal GraphSaveRelationShape Shape { get; }
    Type IGraphSaveRelation.SourceType => typeof(T);
    Type IGraphSaveRelation.TargetType => typeof(TTarget);
    GraphSaveRelationShape IGraphSaveRelation.Shape => Shape;

    IReadOnlyList<object> IGraphSaveRelation.Capture(object source)
    {
        if (source is not T typed) throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
        var supplied = _targets(typed);
        if (supplied is null) throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        var captured = supplied.ToArray();
        if (captured.Any(t => t is null)) throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        if (Shape.Kind == GraphSaveRelationKind.Reference && captured.Length > 1)
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
        return Array.AsReadOnly(captured.Cast<object>().ToArray());
    }

    bool IGraphSaveRelation.Equivalent(IGraphSaveRelation other) =>
        other is GraphSaveRelationBinding<T, TTarget> typed && Shape == typed.Shape && _targets.Equals(typed._targets);
}
