namespace Neemle.OrmLite;

internal interface IGraphSaveRelation
{
    Type SourceType { get; }
    Type TargetType { get; }
    GraphSaveRelationShape Shape { get; }
    IReadOnlyList<object> Capture(object source);
    bool Equivalent(IGraphSaveRelation other);
}
