namespace Neemle.OrmLite;

public sealed record LoadingScopeOptions(GraphLimits Limits, int MaxPendingDemands, TimeSpan Lifetime);
