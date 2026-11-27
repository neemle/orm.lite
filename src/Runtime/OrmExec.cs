#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public static class OrmExec
{
    public static object?[] Row(params object?[] values) => values;

    public static async Task InsertAsync(OrmContext db, EntityDescriptor d, object?[] values)
    {
        ArgumentNullException.ThrowIfNull(db);
        await using var writer = await db.Provider.EnterWriteAsync(db.DataSource);
        await using var conn = await db.DataSource.OpenConnectionAsync();
        await InsertCoreAsync(db, conn, d, values);
    }

    public static async Task InsertAsync(OrmContext db, DbConnection conn, EntityDescriptor d, object?[] values)
    {
        await using var writer = await GateAsync(db, conn);
        await InsertCoreAsync(db, conn, d, values);
    }

    private static async Task InsertCoreAsync(OrmContext db, DbConnection conn, EntityDescriptor d, object?[] values)
    {
        await using var cmd = Command(conn, Compile(db, EntityStatements.Insert(d, values), StatementKind.Write, d.Table
            ));
        await cmd.RunNonQueryAsync(StatementKind.Write);
    }

    public static async Task UpdateAsync(OrmContext db, EntityDescriptor d, object?[] values)
    {
        ArgumentNullException.ThrowIfNull(db);
        await using var writer = await db.Provider.EnterWriteAsync(db.DataSource);
        await using var conn = await db.DataSource.OpenConnectionAsync();
        await UpdateCoreAsync(db, conn, d, values);
    }

    public static async Task UpdateAsync(OrmContext db, DbConnection conn, EntityDescriptor d, object?[] values)
    {
        await using var writer = await GateAsync(db, conn);
        await UpdateCoreAsync(db, conn, d, values);
    }

    private static async Task UpdateCoreAsync(OrmContext db, DbConnection conn, EntityDescriptor d, object?[] values)
    {
        await using var cmd = Command(conn, Compile(db, EntityStatements.Update(d, values), StatementKind.Write, d.Table
            ));
        await cmd.RunNonQueryAsync(StatementKind.Write);
    }

    public static async Task<T?> GetAsync<T>(OrmContext db, EntityDescriptor d, object? key, Func<DbDataReader, T> map)
            where T : class
    {
        ArgumentNullException.ThrowIfNull(db);
        if (PolicyReadHooks<T>.Snapshot is not null)
        {
            RequirePolicyDescriptor(d, map);
            return await PolicyMaterializer.GetAsync(db, null, d, key, map);
        }
        await using var conn = await db.DataSource.OpenConnectionAsync();
        return await GetAsync(db, conn, d, key, map);
    }

    public static async Task<T?> GetAsync<T>(OrmContext db, DbConnection conn, EntityDescriptor d,
        object? key, Func<DbDataReader, T> map) where T : class
    {
        if (PolicyReadHooks<T>.Snapshot is not null)
        {
            RequirePolicyDescriptor(d, map);
            return await PolicyMaterializer.GetAsync(db, conn, d, key, map);
        }
        await using var cmd = Command(conn, Compile(db, EntityStatements.Get(d, key), StatementKind.Read, d.Table));
        await using var r = await cmd.RunReaderAsync(StatementKind.Read);
        if (!await r.ReadAsync()) return null;
        var entity = map(r);
        GraphSaveOrigins.Observe(entity, map, !ReferenceEquals(OrmMeta.ReadDescriptor(typeof(T)), d));
        OrphanReadOrigins.Observe(entity, map, !ReferenceEquals(OrmMeta.ReadDescriptor(typeof(T)), d));
        if (ReferenceEquals(OrmMeta.ReadDescriptor(typeof(T)), d))
            OneToOneProvenance.MarkIfEligible(entity, map, false);
        return entity;
    }

    public static async Task<int> DeleteAsync(OrmContext db, EntityDescriptor d, object? key)
    {
        ArgumentNullException.ThrowIfNull(db);
        await using var writer = await db.Provider.EnterWriteAsync(db.DataSource);
        await using var conn = await db.DataSource.OpenConnectionAsync();
        return await DeleteCoreAsync(db, conn, d, key);
    }

    public static async Task<int> DeleteAsync(OrmContext db, DbConnection conn, EntityDescriptor d, object? key)
    {
        await using var writer = await GateAsync(db, conn);
        return await DeleteCoreAsync(db, conn, d, key);
    }

    private static async Task<int> DeleteCoreAsync(OrmContext db, DbConnection conn, EntityDescriptor d, object? key)
    {
        await using var cmd = Command(conn, Compile(db, EntityStatements.Delete(d, key), StatementKind.Write, d.Table));
        return await cmd.RunNonQueryAsync(StatementKind.Write);
    }

    public static async Task<List<T>> LoadLinkedAsync<T>(
        OrmContext db, DbConnection conn, EntityDescriptor d, string navigation, object? thisKey, Func<DbDataReader, T>
            map)
    {
        var link = LinkOf(d, navigation);
        var list = new List<T>();
        await using var cmd = Command(conn, Compile(db, EntityStatements.LoadLinked(d, link, thisKey), StatementKind.
            Read, link.OtherTable));
        await using var r = await cmd.RunReaderAsync(StatementKind.Read);
        while (await r.ReadAsync())
        {
            var entity = map(r);
            GraphSaveOrigins.Observe(entity, map, OrmMeta.ReadDescriptor(typeof(T))?.Table != link.OtherTable);
            OrphanReadOrigins.Observe(entity, map, true);
            list.Add(entity);
        }
        return list;
    }

    public static Task<List<T>> LoadLinkedAsync<T>(OrmContext db, DbConnection conn, LinkedRead<T> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (PolicyReadHooks<T>.Snapshot is { } policy)
        {
            RequirePolicyDescriptor(read.Target, read.Map, LinkOf(read.Owner, read.Navigation).OtherTable);
            return policy(PolicyMaterializer.Linked(db, conn, read, new RelationReadOptions()),
                new RelationReadOptions(), default);
        }
        var link = LinkOf(read.Owner, read.Navigation);
        var tree = EntityStatements.LoadLinked(read.Owner, link, read.Key, read.Target);
        var state = new RenderState(db.Provider, link.OtherTable, read.Target.ColumnOf, []);
        var executor = new StatementExecutor(db.DataSource, state);
        executor.On(conn, null);
        return executor.RowsAsync(Compile(db, tree, StatementKind.Read, link.OtherTable),
            ReferenceEquals(OrmMeta.ReadDescriptor(typeof(T)), read.Target) && link.OtherTable == read.Target.Table
                ? OneToOneReadMap.Wrap(read.Map, false) : OrphanReadOrigins.Wrap(read.Map, true));
    }

    public static Task<List<T>> LoadLinkedAsync<T>(OrmContext db, DbConnection conn, LinkedRead<T> read,
        RelationReadOptions options, System.Threading.CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(options);
        RequirePolicyDescriptor(read.Target, read.Map, LinkOf(read.Owner, read.Navigation).OtherTable);
        return PolicyReader<T>.SnapshotAsync(PolicyMaterializer.Linked(db, conn, read, options), options, ct);
    }

    private static void RequirePolicyDescriptor<T>(EntityDescriptor descriptor, Func<DbDataReader, T> map,
        string? table = null)
    {
        if (OneToOneReadGuard.Eligible(typeof(T), map)
            && (!ReferenceEquals(OrmMeta.ReadDescriptor(typeof(T)), descriptor)
                || table is not null && table != descriptor.Table))
            throw new GraphRequestException(GraphRequestError.UnsupportedShape);
    }

    public static async Task AddLinkAsync(OrmContext db, DbConnection conn, EntityDescriptor d, string navigation,
            object? thisId, object? otherId)
    {
        var link = LinkOf(d, navigation);
        await using var writer = await GateAsync(db, conn);
        await using var cmd = Command(conn, Compile(db, EntityStatements.AddLink(link, thisId, otherId), StatementKind.
            Write, link.JoinTable));
        await cmd.RunNonQueryAsync(StatementKind.Write);
    }

    public static async Task RemoveLinkAsync(OrmContext db, DbConnection conn, EntityDescriptor d, string navigation,
            object? thisId, object? otherId)
    {
        var link = LinkOf(d, navigation);
        await using var writer = await GateAsync(db, conn);
        await using var cmd = Command(conn, Compile(db, EntityStatements.RemoveLink(link, thisId, otherId),
            StatementKind.Write, link.JoinTable));
        await cmd.RunNonQueryAsync(StatementKind.Write);
    }

    private static async ValueTask<IAsyncDisposable?> GateAsync(OrmContext db, DbConnection conn)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(conn);
        return UnitOfWork.IsOpenOn(conn) ? null : await db.Provider.EnterWriteAsync(db.DataSource);
    }

    private static ManyToManyDescriptor LinkOf(EntityDescriptor d, string navigation)
    {
        foreach (var link in d.ManyToMany)
            if (string.Equals(link.Navigation, navigation, StringComparison.Ordinal)) return link;
        throw new InvalidOperationException(d.ClrType + " has no many-to-many navigation named " + navigation + ".");
    }

    private static Statement Compile(OrmContext db, Node tree, StatementKind kind, string table)
    {
        ArgumentNullException.ThrowIfNull(db);
        return StatementCompiler.Compile(tree, db.Provider, kind, table);
    }

    private static DbCommand Command(DbConnection conn, Statement statement)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = statement.Text;
        foreach (var parameter in statement.Parameters) cmd.AddParam(parameter.Name, parameter.Value);
        return cmd;
    }
}
