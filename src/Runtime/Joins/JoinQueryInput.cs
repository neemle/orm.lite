using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed record JoinQueryInput(OrmContext Database, Select Root, Delegate Mapper,
    StreamBorrowed? Borrowed, bool Unsupported)
{
    internal bool Masked { get; init; }
    internal bool IncludingDeleted { get; init; }
    internal Func<bool> HasPolicy { get; init; } = static () => false;
}
