using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class ScopeBinding(OrmContext context, DbConnection connection, DbTransaction? transaction,
    LoadingScopeOptions options)
{
    internal OrmContext Context { get; } = context;
    internal DbConnection Connection { get; } = connection;
    internal DbTransaction? Transaction { get; } = transaction;
    internal LoadingScopeOptions Options { get; } = options;
    internal UnitOfWork? Unit { get; init; }
    internal bool OwnsConnection { get; init; }

    internal void Validate()
    {
        try
        {
            if (Connection.State != ConnectionState.Open || Transaction is { } tx && tx.Connection != Connection)
                throw new RelationScopeException(RelationScopeError.InvalidResource);
            if (Unit is { } unit && (unit.Committed || unit.Connection != Connection || unit.Transaction != Transaction))
                throw new RelationScopeException(RelationScopeError.InvalidResource);
        }
        catch (ObjectDisposedException) { throw new RelationScopeException(RelationScopeError.InvalidResource); }
    }

    internal void Match<T>(GraphQueryState<T> state)
    {
        if (!ReferenceEquals(state.Database, Context)
            || state.Connection is { } conn && (!ReferenceEquals(conn, Connection)
                || !ReferenceEquals(state.Transaction, Transaction)))
            throw new RelationScopeException(RelationScopeError.DifferentScope);
        Validate();
    }
}
