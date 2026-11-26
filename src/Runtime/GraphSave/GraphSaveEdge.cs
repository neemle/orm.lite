namespace Neemle.OrmLite;

internal sealed record GraphSaveEdge(GraphSaveNode Source, GraphSaveNode Target,
    IGraphSaveRelation Binding, GraphSaveRule Rule);
