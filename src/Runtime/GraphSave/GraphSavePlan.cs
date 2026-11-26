namespace Neemle.OrmLite;

public sealed record GraphSavePlan(GraphSaveIntents Intents, GraphSavePermissions Permissions,
    GraphSaveLimits Limits, GraphSaveTransactionMode TransactionMode = GraphSaveTransactionMode.Owned);
