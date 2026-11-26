namespace Neemle.OrmLite;

internal sealed record GraphSaveSnapshot(IReadOnlyList<GraphSaveNode> Nodes, IReadOnlyList<GraphSaveEdge> Edges);
