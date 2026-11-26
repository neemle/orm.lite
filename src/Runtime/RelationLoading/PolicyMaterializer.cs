using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class PolicyMaterializer
{
    internal static async Task<T?> GetAsync<T>(OrmContext db, DbConnection? connection, EntityDescriptor descriptor,
        object? key, Func<DbDataReader, T> map) where T : class
    {
        var state = new GraphQueryState<T>(db, (Select)EntityStatements.Get(descriptor, key), map)
            { Connection = connection };
        var rows = await PolicyReader<T>.SnapshotAsync(state, new RelationReadOptions(), default);
        return rows.FirstOrDefault();
    }

    internal static GraphQueryState<T> Linked<T>(OrmContext db, DbConnection connection, LinkedRead<T> read,
        RelationReadOptions options)
    {
        var link = read.Owner.ManyToMany.Single(m => m.Navigation == read.Navigation);
        var tree = (Select)EntityStatements.LoadLinked(read.Owner, link, read.Key, read.Target);
        if (options.Scope is { } scope && !ReferenceEquals(scope.Binding.Connection, connection))
            throw new RelationScopeException(RelationScopeError.DifferentScope);
        return new GraphQueryState<T>(db, tree, read.Map)
            { Connection = connection, Transaction = options.Scope?.Binding.Transaction };
    }
}
