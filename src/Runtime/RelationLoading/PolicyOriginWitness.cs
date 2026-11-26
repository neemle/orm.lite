using System.Data;
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
        RequireSource(context);
        return RequireUnit(connection, transaction);
    }

    private void RequireSource(OrmContext context)
    {
        if (Volatile.Read(ref _closed) != 0 || !_source.TryGetTarget(out var source)
            || !_provider.TryGetTarget(out var provider)) throw Invalid();
        if (!ReferenceEquals(source, context.DataSource) || !ReferenceEquals(provider, context.Provider))
            throw new RelationScopeException(RelationScopeError.DifferentScope);
    }

    private UnitOfWork? RequireUnit(DbConnection connection, DbTransaction? transaction)
    {
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
