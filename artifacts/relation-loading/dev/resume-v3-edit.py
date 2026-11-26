from pathlib import Path
p=Path('src/Runtime/RelationLoading')
(p/'PolicyOriginWitness.cs').write_text('''using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class PolicyOriginWitness
{
    private readonly WeakReference<DbDataSource> _source;
    private readonly WeakReference<ISqlProvider> _provider;
    private readonly WeakReference<UnitOfWork>? _unit;
    private int _closed;

    internal PolicyOriginWitness(OrmContext context, UnitOfWork? unit)
    {
        _source = new(context.DataSource);
        _provider = new(context.Provider);
        _unit = unit is null ? null : new(unit);
    }

    internal void Observe(object? sender, StateChangeEventArgs change)
    {
        if (change.CurrentState is ConnectionState.Closed or ConnectionState.Broken)
            Interlocked.Exchange(ref _closed, 1);
    }

    internal UnitOfWork? Require(OrmContext context, DbConnection connection, DbTransaction? transaction)
    {
        if (Volatile.Read(ref _closed) != 0 || !_source.TryGetTarget(out var source)
            || !_provider.TryGetTarget(out var provider)) throw Invalid();
        if (!ReferenceEquals(source, context.DataSource) || !ReferenceEquals(provider, context.Provider))
            throw new RelationScopeException(RelationScopeError.DifferentScope);
        if (_unit is null) return null;
        if (!_unit.TryGetTarget(out var unit) || unit.Committed || unit.PolicyLifetime.IsClosed) throw Invalid();
        try
        {
            if (unit.Connection != connection || unit.Transaction != transaction)
                throw new RelationScopeException(RelationScopeError.DifferentScope);
        }
        catch (ObjectDisposedException) { throw Invalid(); }
        return unit;
    }

    private static RelationScopeException Invalid() => new(RelationScopeError.InvalidResource);
}
''')
(p/'PolicyOrigins.cs').write_text('''using System.Data.Common;
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
            if (Origins.TryGetValue(connection, out var witness)) return witness.Require(context, connection, transaction);
            throw new RelationScopeException(RelationScopeError.InvalidResource);
        }
    }
}
''')
f=p/'PolicyLifetime.cs';s=f.read_text().replace('    internal void Add(LoadingScope scope)','    internal bool IsClosed { get { lock (_gate) return _closed; } }\n\n    internal void Add(LoadingScope scope)');f.write_text(s)
f=Path('src/Runtime/UnitOfWork.cs');s=f.read_text().replace('''            PolicyOrigins.Record(connection, context);
            return new UnitOfWork(connection, started, context.Provider, TransactionTrace.Begin(connection));''','''            var unit = new UnitOfWork(connection, started, context.Provider, TransactionTrace.Begin(connection));
            PolicyOrigins.Record(connection, context, unit);
            return unit;''');f.write_text(s)
f=p/'LoadingScope.cs';s=f.read_text().replace('PolicyOrigins.Require(binding.Connection, context);','PolicyOrigins.Require(binding.Connection, context, binding.Transaction);')
old='''        var binding = new ScopeBinding(context, connection, transaction, options);
        PolicyOrigins.Require(binding.Connection, context, binding.Transaction);''';new='''        var unit = PolicyOrigins.Require(connection, context, transaction);
        var binding = new ScopeBinding(context, connection, transaction, options) { Unit = unit };''';assert old in s;s=s.replace(old,new);f.write_text(s)
f=p/'ScopeBinding.cs';s=f.read_text().replace('unit.Committed || unit.Connection','unit.Committed || unit.PolicyLifetime.IsClosed || unit.Connection');f.write_text(s)
(p/'PolicyBoundFirstReader.cs').write_text('''using System.Data.Common;

namespace Neemle.OrmLite;

internal static class PolicyBoundFirstReader<T> where T : class
{
    internal static async Task<T?> ReadAsync(Query<T> query, DbConnection connection,
        RelationReadOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();
        var snapshot = query.GraphFirstSnapshot();
        options = options with { };
        var bound = Bind(snapshot, connection, options);
        var rows = await PolicyReader<T>.SnapshotAsync(bound, options, ct);
        return rows.FirstOrDefault();
    }

    private static GraphQueryState<T> Bind(GraphQueryState<T> snapshot, DbConnection connection,
        RelationReadOptions options)
    {
        if (snapshot.Connection is { } explicitConnection && !ReferenceEquals(explicitConnection, connection))
            throw new RelationScopeException(RelationScopeError.DifferentScope);
        var transaction = snapshot.Transaction;
        if (options.Scope is { } scope)
        {
            if (!ReferenceEquals(scope.Binding.Connection, connection))
                throw new RelationScopeException(RelationScopeError.DifferentScope);
            scope.Binding.Match(snapshot);
            if (snapshot.Connection is null) transaction = scope.Binding.Transaction;
        }
        return new GraphQueryState<T>(snapshot.Database, snapshot.Root, snapshot.Map)
        {
            Connection = connection, Transaction = transaction,
            Masks = snapshot.Masks, Unsupported = snapshot.Unsupported,
        };
    }
}
''')
f=p/'RelationQueryExtensions.cs';s=f.read_text();pos=s.rindex('}');s=s[:pos]+'''    public static Task<T?> FirstOrDefaultAsync<T>(this Query<T> query,
        System.Data.Common.DbConnection connection, RelationReadOptions options, CancellationToken ct) where T : class =>
        PolicyBoundFirstReader<T>.ReadAsync(query, connection, options, ct);
'''+s[pos:];f.write_text(s)
f=Path('src/Generator/RelationLoading/PolicyKeyedEmitter.cs');s=f.read_text().replace('''            var on = bound ? ".On(conn)" : "";''','''            var arguments = bound ? ", conn, options, ct);" : ", options, ct);";''').replace('''                + Runtime + "RelationQueryExtensions.FirstOrDefaultAsync(Query(db)" + on + ".Where(e => e."
                + Identifier(key.Name) + " == key), options, ct);";''','''                + Runtime + "RelationQueryExtensions.FirstOrDefaultAsync(Query(db).Where(e => e."
                + Identifier(key.Name) + " == key)" + arguments;''');f.write_text(s)
