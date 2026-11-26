using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Neemle.OrmLite;

internal static class PolicyOrigins
{
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<DbConnection, PolicyOriginWitness> Origins = new();

    internal static void Record(DbConnection connection, OrmContext context, UnitOfWork? unit = null)
    {
        lock (Gate)
        {
            if (Origins.TryGetValue(connection, out var previous)) connection.StateChange -= previous.Observe;
            var witness = new PolicyOriginWitness(context, unit);
            Origins.AddOrUpdate(connection, witness);
            connection.StateChange += witness.Observe;
        }
    }

    internal static UnitOfWork? Require(DbConnection connection, OrmContext context, DbTransaction? transaction)
    {
        lock (Gate)
        {
            if (Origins.TryGetValue(connection, out var witness)) return witness.Require(context, connection,
            transaction);
            throw new RelationScopeException(RelationScopeError.InvalidResource);
        }
    }
}
