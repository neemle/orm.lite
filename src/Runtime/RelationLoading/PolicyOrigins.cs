using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Neemle.OrmLite;

internal static class PolicyOrigins
{
    private static readonly ConditionalWeakTable<DbConnection, OrmContext> Origins = new();

    internal static void Record(DbConnection connection, OrmContext context) => Origins.AddOrUpdate(connection, context);

    internal static void Require(DbConnection connection, OrmContext context)
    {
        if (!Origins.TryGetValue(connection, out var actual))
            throw new RelationScopeException(RelationScopeError.InvalidResource);
        if (!ReferenceEquals(actual, context)) throw new RelationScopeException(RelationScopeError.DifferentScope);
    }
}
