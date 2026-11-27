namespace Neemle.OrmLite;

public sealed record GraphSaveRule(GraphSaveActions Actions,
    GraphSaveKeyBinding Keys = GraphSaveKeyBinding.RequireMatch);
