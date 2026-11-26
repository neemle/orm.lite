using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public static class OneToOneRows
{
    public static async Task<T?> ReadAsync<T, TKey>(OneToOneReadScope scope, OneToOneLookup<T, TKey> lookup,
        CancellationToken ct = default) where T : class where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(lookup);
        ct.ThrowIfCancellationRequested();
        OneToOneLookupValidation.Require(lookup);
        RequireResources(scope);
        var statement = StatementCompiler.Compile(Tree(lookup), scope.Context.Provider,
            StatementKind.Read, lookup.Target.Entity.Table);
        return await ExecuteAsync(scope, lookup, statement, ct);
    }

    private static Select Tree<T, TKey>(OneToOneLookup<T, TKey> lookup) where T : class where TKey : notnull
    {
        var target = lookup.Target;
        object key = lookup.Key is Enum value ? value.ToString() : lookup.Key;
        var terms = new List<Node> { new Compare(CompareOp.Equal,
            new ColumnRef(target.MatchColumn), new Param(0, key, "key")) };
        if (target.Entity.Soft is { } soft) terms.Add(new IsNull(new ColumnRef(soft.Column)));
        return new Select(NodeList.Of(EntityProjection.Selected(target.Entity)), new TableRef(target.Entity.Table),
            new Where(NodeList.Of(terms)), Limit: new Limit(2));
    }

    private static void RequireResources(OneToOneReadScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope.Context);
        ArgumentNullException.ThrowIfNull(scope.Connection);
        if (scope.Connection.State != System.Data.ConnectionState.Open
            || scope.Transaction is { } tx && !ReferenceEquals(tx.Connection, scope.Connection))
            throw new OneToOneDeclarationException(OneToOneError.UnsupportedSchema, null);
    }

    private static async Task<T?> ExecuteAsync<T, TKey>(OneToOneReadScope scope, OneToOneLookup<T, TKey> lookup,
        Statement statement, CancellationToken ct) where T : class where TKey : notnull
    {
        DbCommand? command = null;
        DbDataReader? reader = null;
        Exception? primary = null;
        try
        {
            command = StatementCommand.Create(scope.Context.DataSource, scope.Connection, scope.Transaction, statement);
            foreach (var p in statement.Parameters) command.AddParam(p.Name, p.Value);
            reader = await command.RunReaderAsync(StatementKind.Read, ct);
            return await StageAsync(reader, lookup, ct);
        }
        catch (Exception error) { primary = error; throw; }
        finally { await OneToOneReadCleanup.DisposeAsync(reader, command, primary); }
    }

    private static async Task<T?> StageAsync<T, TKey>(DbDataReader reader, OneToOneLookup<T, TKey> lookup,
        CancellationToken ct) where T : class where TKey : notnull
    {
        if (!await reader.ReadAsync(ct)) return null;
        var entity = lookup.Map(reader);
        if (await reader.ReadAsync(ct))
            throw new OneToOneCardinalityException(lookup.Target.Entity.ClrType, lookup.Target.Navigation);
        OneToOneProvenance.MarkIfEligible(entity, lookup.Map, false);
        return entity;
    }
}
