using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class GraphReadContext(OrmContext database, DbConnection connection,
    DbTransaction? transaction, GraphLimits limits, CancellationToken cancellationToken)
{
    internal OrmContext Database { get; } = database;
    internal DbConnection Connection { get; } = connection;
    internal DbTransaction? Transaction { get; } = transaction;
    internal CancellationToken CancellationToken { get; } = cancellationToken;
    internal GraphLimits Limits { get; } = limits;
    internal GraphBudgetCounter Budget { get; } = new(limits);
    internal GraphIdentityMap Identity { get; } = new();
    internal Action<object, IGraphNavigationBinding, object?, Action>? Publication { get; init; }

    internal void Assign(object parent, IGraphNavigationBinding edge, object? value, Action publish)
    {
        if (Publication is { } stage) stage(parent, edge, value, publish);
        else publish();
    }
}
