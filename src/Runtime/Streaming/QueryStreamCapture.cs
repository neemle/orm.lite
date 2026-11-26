using System.Data.Common;
using System.Linq.Expressions;

namespace Neemle.OrmLite;

internal static class QueryStreamCapture
{
    internal static StreamReadSnapshot<T> Entities<T>(Query<T> query) =>
        Capture(query, query.CompileSelect, query.StreamMap);

    internal static StreamReadSnapshot<TOut> Project<T, TOut>(Query<T> query,
        IReadOnlyList<Expression<Func<T, object?>>> columns, Func<DbDataReader, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(map);
        var copied = columns.ToArray();
        return Capture(query, () => query.CompileProject(copied), map);
    }

    internal static StreamReadSnapshot<TOut> Projection<T, TOut>(Query<T> query, Func<DbDataReader, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return Capture(query, query.CompileProjection, map);
    }

    internal static StreamReadSnapshot<TCol> Column<T, TCol>(Query<T> query, Expression<Func<T, object?>> column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return Capture(query, () => query.CompileProject([column]), ScalarMap<TCol>(query.SourceTable));
    }

    private static Func<DbDataReader, TCol> ScalarMap<TCol>(string table) =>
        r => ScalarValue.To<TCol>(r.IsDBNull(0) ? null : r.GetValue(0), table, "stream column");

    private static StreamReadSnapshot<TOut> Capture<T, TOut>(Query<T> query, Func<Statement> compile,
        Func<DbDataReader, TOut> map)
    {
        try
        {
            var statement = compile();
            var binding = query.StreamBinding;
            var borrowed = query.StreamBorrowed;
            var rowLock = query.StreamRowLock;
            Validate(statement, binding);
            if (rowLock is not null && borrowed?.Transaction is null)
                throw new QueryStreamException(QueryStreamError.ResourceMismatch);
            if (rowLock is not null && borrowed is not null) StreamResourceChecks.Validate(binding, borrowed);
            StreamNativeLocks.Validate(rowLock, binding.Provider);
            return new StreamReadSnapshot<TOut>(statement, map, binding, borrowed);
        }
        catch (QueryStreamException) { throw; }
        catch (Exception error) { throw new QueryStreamException(QueryStreamError.InvalidRequest, cause: error); }
    }

    private static void Validate(Statement statement, StreamBinding binding)
    {
        if (statement.Kind != StatementKind.Read)
            throw new QueryStreamException(QueryStreamError.UnsupportedShape);
        if (binding.DataSource is null) throw new QueryStreamException(QueryStreamError.InvalidRequest);
        if (statement.Parameters.Count > binding.Provider.MaxBindParameters)
            throw new QueryStreamException(QueryStreamError.ParameterCapacity);
        if (binding.DataSource is GatedDataSource gate && !ReferenceEquals(gate.Provider, binding.Provider))
            throw new QueryStreamException(QueryStreamError.ResourceMismatch);
    }
}
