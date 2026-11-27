namespace Neemle.OrmLite;

internal sealed record JoinPlanEdge(QueryJoinSnapshot Source, JoinPredicate On, bool Left);
