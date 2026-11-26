using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class GraphReadContext(OrmContext database, DbConnection connection,
    DbTransaction? transaction, GraphLimits limits, CancellationToken cancellationToken)
{
    internal OrmContext Database { get; } = database;
    internal DbConnection Connection { get; } = connection;
    internal DbTransaction? Transaction { get; } = transaction;
    internal CancellationToken CancellationToken { get; } = cancellationToken;
    internal GraphBudgetCounter Budget { get; } = new(limits);
    internal GraphIdentityMap Identity { get; } = new();
}
