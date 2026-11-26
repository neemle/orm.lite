namespace Neemle.OrmLite;

internal interface IGraphNavigationBinding
{
    Type Source { get; }
    Type Target { get; }
    string Navigation { get; }
    string? SourceColumn { get; }
    string? TargetColumn { get; }
    Type SourceKeyType { get; }
    Type TargetKeyType { get; }
    GraphLinkSource? Link { get; }
    object? CaptureSourceKey(object source);
    bool Equivalent(IGraphNavigationBinding other);
    void Validate(IGraphEntityBinding source, IGraphEntityBinding target);
    Task<List<object>> LoadAsync(GraphReadContext context, GraphPlanNode node, IReadOnlyList<object> parents);
}
