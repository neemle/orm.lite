using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed record JoinInput(OrmContext Database, Select Root, Delegate Mapper,
    StreamBorrowed? Borrowed, bool Unsupported)
{
    internal bool Masked { get; init; }
    internal bool IncludingDeleted { get; init; }
    internal Func<bool> HasPolicy { get; init; } = static () => false;
    internal static JoinInput Of<T>(GraphQueryState<T> state, StreamBorrowed? borrowed, int aggregates, bool live) =>
        new(state.Database, state.Root, state.Map, borrowed, state.Unsupported || aggregates != 0)
        {
            Masked = state.Masks.Count != 0,
            IncludingDeleted = !live,
            HasPolicy = Policies<T>,
        };

    private static bool Policies<T>() => RelationPolicies.Find(typeof(T)) is not null
        || PolicyReadHooks<T>.Rows is not null || PolicyReadHooks<T>.First is not null
        || PolicyReadHooks<T>.Snapshot is not null;
}
