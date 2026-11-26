from pathlib import Path
p=Path('src/Runtime/RelationLoading/PolicyReadHooks.cs');s=p.read_text().replace('    internal static Func<Query<T>', '    internal static Func<Query<T>',1).replace('    internal static Func<Query<T>, RelationReadOptions, CancellationToken, Task<T?>>? First;','''    internal static Func<Query<T>, RelationReadOptions, CancellationToken, Task<T?>>? First;
    internal static Func<GraphQueryState<T>, RelationReadOptions, CancellationToken, Task<List<T>>>? Snapshot;''');p.write_text(s)
p=Path('src/Runtime/RelationLoading/RelationPolicies.cs');s=p.read_text().replace('''            PolicyReadHooks<T>.First = PolicyReader<T>.FirstAsync;''','''            PolicyReadHooks<T>.First = PolicyReader<T>.FirstAsync;
            PolicyReadHooks<T>.Snapshot = PolicyReader<T>.SnapshotAsync;''');p.write_text(s)
p=Path('src/Runtime/RelationLoading/PolicyReader.cs');s=p.read_text().replace('    private static async Task<List<T>> ReadAsync(','''    internal static Task<List<T>> SnapshotAsync(GraphQueryState<T> state, RelationReadOptions options,
        CancellationToken ct) => ReadAsync(state, options, ct);

    private static async Task<List<T>> ReadAsync(''');p.write_text(s)
p=Path('src/Runtime/RelationLoading/PolicyMaterializer.cs');p.write_text('''using System.Data.Common;
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
''')
p=Path('src/Runtime/OrmExec.cs');s=p.read_text();old='''        ArgumentNullException.ThrowIfNull(db);
        await using var conn = await db.DataSource.OpenConnectionAsync();
        return await GetAsync(db, conn, d, key, map);''';new='''        ArgumentNullException.ThrowIfNull(db);
        if (PolicyReadHooks<T>.Snapshot is not null)
            return await PolicyMaterializer.GetAsync(db, null, d, key, map);
        await using var conn = await db.DataSource.OpenConnectionAsync();
        return await GetAsync(db, conn, d, key, map);''';assert old in s;s=s.replace(old,new)
old='''        await using var cmd = Command(conn, Compile(db, EntityStatements.Get(d, key), StatementKind.Read, d.Table));''';new='''        if (PolicyReadHooks<T>.Snapshot is not null)
            return await PolicyMaterializer.GetAsync(db, conn, d, key, map);
        await using var cmd = Command(conn, Compile(db, EntityStatements.Get(d, key), StatementKind.Read, d.Table));''';assert old in s;s=s.replace(old,new)
old='''        var link = LinkOf(d, navigation);
        var list = new List<T>();''';new='''        var link = LinkOf(d, navigation);
        if (PolicyReadHooks<T>.Snapshot is { } policy)
            return await policy(new GraphQueryState<T>(db, (Select)EntityStatements.LoadLinked(d, link, thisKey), map)
                { Connection = conn }, new RelationReadOptions(), default);
        var list = new List<T>();''';assert old in s;s=s.replace(old,new)
old='''        ArgumentNullException.ThrowIfNull(read);
        var link = LinkOf(read.Owner, read.Navigation);''';new='''        ArgumentNullException.ThrowIfNull(read);
        if (PolicyReadHooks<T>.Snapshot is { } policy)
            return policy(PolicyMaterializer.Linked(db, conn, read, new RelationReadOptions()),
                new RelationReadOptions(), default);
        var link = LinkOf(read.Owner, read.Navigation);''';assert old in s;s=s.replace(old,new)
pos=s.index('    public static async Task AddLinkAsync')
s=s[:pos]+'''    public static Task<List<T>> LoadLinkedAsync<T>(OrmContext db, DbConnection conn, LinkedRead<T> read,
        RelationReadOptions options, System.Threading.CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(options);
        return PolicyReader<T>.SnapshotAsync(PolicyMaterializer.Linked(db, conn, read, options), options, ct);
    }

'''+s[pos:];p.write_text(s)
