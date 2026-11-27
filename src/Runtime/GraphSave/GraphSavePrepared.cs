namespace Neemle.OrmLite;

internal sealed record GraphSavePrepared(GraphSaveSnapshot Snapshot, IReadOnlyList<GraphSaveNode> Writes,
    IReadOnlyList<GraphSaveNode> Anchors, IReadOnlyList<GraphSaveLink> Links);
